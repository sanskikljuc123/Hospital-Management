import contract from "../../../contracts/openapi.json";

/**
 * The API contract check of the component tests (R-061, TEST_STRATEGY §2). `contracts/openapi.json` is the API's
 * own OpenAPI description, kept equal to the real API by `OpenApiContractTests` in the API tests. Every request a
 * component test sends and every response a test mocks is checked against it, so the frontend cannot drift from the
 * API unnoticed: a method or path the API does not have, a field of the wrong type, a mock with fields the API does
 * not return.
 *
 * Not described yet, so not checked: error responses (4xx, 5xx) and query parameters.
 */

type Schema = {
  $ref?: string;
  type?: string;
  format?: string;
  nullable?: boolean;
  enum?: unknown[];
  properties?: Record<string, Schema>;
  required?: string[];
  additionalProperties?: boolean | Schema;
  items?: Schema;
  maxLength?: number;
  minLength?: number;
};
type Media = { content?: Record<string, { schema?: Schema }> };
type Operation = { requestBody?: Media; responses?: Record<string, Media> };
type Contract = {
  paths: Record<string, Record<string, Operation>>;
  components?: { schemas?: Record<string, Schema> };
};

const api = contract as unknown as Contract;
const routes = Object.entries(api.paths).map(([template, item]) => ({
  template,
  item,
  // ASP.NET Core routing ignores case: /api/doctor/9 is /api/Doctor/{id}.
  pattern: new RegExp("^" + template.replace(/\{[^}]+\}/g, "[^/]+") + "$", "i"),
}));

function resolve(schema: Schema): Schema {
  if (!schema.$ref) return schema;
  const name = schema.$ref.replace("#/components/schemas/", "");
  const target = api.components?.schemas?.[name];
  if (!target) throw new Error(`contracts/openapi.json: ${schema.$ref} does not exist`);
  return resolve(target);
}

/** Problems of `value` against `schema`, each with the JSON path where it was found. */
export function validate(schema: Schema, value: unknown, at = "$"): string[] {
  const s = resolve(schema);
  if (value === null || value === undefined) return s.nullable || value === undefined ? [] : [`${at} is null`];
  if (s.enum && !s.enum.includes(value)) return [`${at} is ${JSON.stringify(value)}, not one of ${JSON.stringify(s.enum)}`];
  switch (s.type) {
    case "object": {
      if (typeof value !== "object" || Array.isArray(value)) return [`${at} should be an object`];
      const record = value as Record<string, unknown>;
      const problems = (s.required ?? []).filter((key) => record[key] === undefined).map((key) => `${at}.${key} is required`);
      for (const [key, item] of Object.entries(record)) {
        const property = s.properties?.[key];
        if (property) problems.push(...validate(property, item, `${at}.${key}`));
        else if (s.additionalProperties === false) problems.push(`${at}.${key} is not a field of this API type`);
      }
      return problems;
    }
    case "array":
      if (!Array.isArray(value)) return [`${at} should be an array`];
      return value.flatMap((item, index) => (s.items ? validate(s.items, item, `${at}[${index}]`) : []));
    case "string":
      if (typeof value !== "string") return [`${at} should be a string, is ${JSON.stringify(value)}`];
      if (s.maxLength !== undefined && value.length > s.maxLength) return [`${at} is longer than ${s.maxLength}`];
      if (s.minLength !== undefined && value.length < s.minLength) return [`${at} is shorter than ${s.minLength}`];
      return [];
    case "integer":
      return Number.isInteger(value) ? [] : [`${at} should be an integer, is ${JSON.stringify(value)}`];
    case "number":
      return typeof value === "number" ? [] : [`${at} should be a number, is ${JSON.stringify(value)}`];
    case "boolean":
      return typeof value === "boolean" ? [] : [`${at} should be a boolean, is ${JSON.stringify(value)}`];
    default:
      return [];
  }
}

function find(method: string, url: string) {
  const path = new URL(url).pathname;
  const route = routes.find((r) => r.pattern.test(path));
  if (!route) return { problem: `${method} ${path}: the API has no such path` };
  const operation = route.item[method.toLowerCase()];
  if (!operation) {
    const allowed = Object.keys(route.item).map((m) => m.toUpperCase()).join(", ");
    return { problem: `${method} ${path}: the API offers ${allowed} on ${route.template}, not ${method}` };
  }
  return { label: `${method} ${route.template}`, operation };
}

async function jsonBody(message: Request | Response): Promise<unknown> {
  const text = await message.clone().text();
  return text ? JSON.parse(text) : undefined;
}

/** Problems of a request the frontend sends. */
export async function checkRequest(request: Request): Promise<string[]> {
  const { problem, label, operation } = find(request.method, request.url);
  if (problem) return [problem];
  const schema = operation!.requestBody?.content?.["application/json"]?.schema;
  const body = await jsonBody(request);
  if (!schema || body === undefined) return [];
  return validate(schema, body).map((p) => `${label} request body: ${p}`);
}

/** Problems of a response a test mocks. Error responses are not in the contract yet and pass unchecked. */
export async function checkMockedResponse(request: Request, response: Response): Promise<string[]> {
  const { problem, label, operation } = find(request.method, request.url);
  if (problem || response.status >= 400) return [];
  const described = operation!.responses ?? {};
  const spec = described[String(response.status)];
  if (!spec) return [`${label} is mocked with ${response.status}; the API answers ${Object.keys(described).join(", ")}`];
  const schema = spec.content?.["application/json"]?.schema;
  const body = await jsonBody(response);
  if (!schema) return body === undefined ? [] : [`${label} ${response.status} has no body in the API, the mock sends one`];
  return validate(schema, body).map((p) => `${label} ${response.status} mocked body: ${p}`);
}
