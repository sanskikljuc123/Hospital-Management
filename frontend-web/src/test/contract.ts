import contract from "../../../contracts/openapi.json";

/**
 * The API contract check of the component tests (R-061, TEST_STRATEGY §2). `contracts/openapi.json` is the API's
 * own OpenAPI description, kept equal to the real API by `OpenApiContractTests` in the API tests. Every request a
 * component test sends and every response a test mocks is checked against it, so the frontend cannot drift from the
 * API unnoticed: a method or path the API does not have, a field of the wrong type, a mock with fields the API does
 * not return.
 *
 * Error responses are checked too: a status the contract describes (400, 404, 409, a 403 with a reason) must be a
 * ProblemDetails body; any other error status must have an empty body, as the API's ownership and role checks
 * answer (401, 403). So are the parameters of the URL: every query parameter must be one the endpoint accepts, with
 * a value of its type, and every path parameter (`{id}`) must have its type. The API silently ignores an unknown
 * query parameter, so `?patient=5` instead of `?patientId=5` would return everyone's data instead of one patient's.
 * Not described, so not checked: 5xx.
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
type Parameter = { name: string; in: "path" | "query" | "header" | "cookie"; required?: boolean; schema?: Schema };
type Operation = { parameters?: Parameter[]; requestBody?: Media; responses?: Record<string, Media> };
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
  if (s.enum && !s.enum.includes(value))
    return [`${at} is ${JSON.stringify(value)}, not one of ${JSON.stringify(s.enum)}`];
  switch (s.type) {
    case "object": {
      if (typeof value !== "object" || Array.isArray(value)) return [`${at} should be an object`];
      const record = value as Record<string, unknown>;
      const problems = (s.required ?? [])
        .filter((key) => record[key] === undefined)
        .map((key) => `${at}.${key} is required`);
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
    const allowed = Object.keys(route.item)
      .map((m) => m.toUpperCase())
      .join(", ");
    return { problem: `${method} ${path}: the API offers ${allowed} on ${route.template}, not ${method}` };
  }
  return { label: `${method} ${route.template}`, template: route.template, operation };
}

const INT32 = { min: -2147483648, max: 2147483647 };
const DATE = /^\d{4}-\d{2}-\d{2}$/;
const DATE_TIME = /^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?)?$/;

/** Problems of one URL parameter value. Values in a URL are text, so the type is checked on the text. */
function checkParameterValue(where: string, name: string, raw: string, schema: Schema | undefined): string[] {
  const s = schema ? resolve(schema) : {};
  const wrong = (expected: string) => [`${where} ${name}=${JSON.stringify(raw)} should be ${expected}`];
  if (s.enum) return s.enum.map(String).includes(raw) ? [] : wrong(`one of ${s.enum.join(", ")}`);
  switch (s.type) {
    case "integer": {
      const ok =
        /^-?\d+$/.test(raw) && (s.format !== "int32" || (Number(raw) >= INT32.min && Number(raw) <= INT32.max));
      return ok ? [] : wrong(s.format === "int32" ? "a 32-bit integer" : "an integer");
    }
    case "number":
      return raw.trim() !== "" && Number.isFinite(Number(raw)) ? [] : wrong("a number");
    case "boolean":
      return /^(true|false)$/i.test(raw) ? [] : wrong("true or false");
    case "string":
      if (s.format === "date-time")
        return DATE_TIME.test(raw) && !Number.isNaN(Date.parse(raw)) ? [] : wrong("an ISO 8601 date-time");
      if (s.format === "date") return DATE.test(raw) && !Number.isNaN(Date.parse(raw)) ? [] : wrong("an ISO 8601 date");
      return [];
    default:
      return [];
  }
}

/**
 * Problems of the URL's path and query parameters: every path parameter has its type, every query parameter is one
 * the endpoint accepts (names compared without case, as ASP.NET Core binds them), appears once unless it is a list,
 * has a value of its type, and none the contract requires is missing.
 */
function checkUrlParameters(label: string, template: string, operation: Operation, url: string): string[] {
  const parsed = new URL(url);
  const parameters = operation.parameters ?? [];
  const problems: string[] = [];

  const templateSegments = template.split("/");
  const actualSegments = parsed.pathname.split("/");
  templateSegments.forEach((segment, index) => {
    const name = /^\{(.+)\}$/.exec(segment)?.[1];
    if (!name) return;
    const parameter = parameters.find((p) => p.in === "path" && p.name === name);
    problems.push(
      ...checkParameterValue(
        `${label} path parameter`,
        name,
        decodeURIComponent(actualSegments[index]),
        parameter?.schema,
      ),
    );
  });

  const accepted = parameters.filter((p) => p.in === "query");
  const counts = new Map<string, number>();
  for (const [key, value] of parsed.searchParams) {
    const parameter = accepted.find((p) => p.name.toLowerCase() === key.toLowerCase());
    if (!parameter) {
      const names = accepted.length ? `it accepts ${accepted.map((p) => p.name).join(", ")}` : "it accepts none";
      problems.push(`${label}: the API has no query parameter "${key}" (${names}); the API ignores it`);
      continue;
    }
    const count = (counts.get(parameter.name) ?? 0) + 1;
    counts.set(parameter.name, count);
    const isList = parameter.schema && resolve(parameter.schema).type === "array";
    if (count === 2 && !isList) problems.push(`${label}: query parameter "${parameter.name}" is sent more than once`);
    const itemSchema = isList ? resolve(parameter.schema!).items : parameter.schema;
    problems.push(...checkParameterValue(`${label} query parameter`, parameter.name, value, itemSchema));
  }
  for (const parameter of accepted.filter((p) => p.required && !counts.has(p.name)))
    problems.push(`${label}: the required query parameter "${parameter.name}" is missing`);
  return problems;
}

/** A body that is not JSON, e.g. the plain-text errors the API sent before ProblemDetails. */
class NotJson {
  constructor(readonly text: string) {}
}

async function jsonBody(message: Request | Response): Promise<unknown> {
  const text = await message.clone().text();
  if (!text) return undefined;
  try {
    return JSON.parse(text);
  } catch {
    return new NotJson(text);
  }
}

function notJson(where: string, body: unknown): string[] {
  return body instanceof NotJson ? [`${where} is not JSON: ${JSON.stringify(body.text.slice(0, 60))}`] : [];
}

/** Problems of a request the frontend sends. */
export async function checkRequest(request: Request): Promise<string[]> {
  const { problem, label, template, operation } = find(request.method, request.url);
  if (problem) return [problem];
  const problems = checkUrlParameters(label!, template!, operation!, request.url);
  const schema = operation!.requestBody?.content?.["application/json"]?.schema;
  const body = await jsonBody(request);
  if (!schema || body === undefined) return problems;
  if (body instanceof NotJson) return [...problems, ...notJson(`${label} request body`, body)];
  return [...problems, ...validate(schema, body).map((p) => `${label} request body: ${p}`)];
}

/** The schema of a described response, whatever its JSON media type (application/json, application/problem+json). */
function responseSchema(spec: Media): Schema | undefined {
  const media = Object.entries(spec.content ?? {}).find(([type]) => type.includes("json"));
  return media?.[1].schema;
}

/** Problems of a response a test mocks. */
export async function checkMockedResponse(request: Request, response: Response): Promise<string[]> {
  const { problem, label, operation } = find(request.method, request.url);
  if (problem || response.status >= 500) return [];
  const described = operation!.responses ?? {};
  const spec = described[String(response.status)];
  const body = await jsonBody(response);
  if (!spec) {
    // 401 and the 403 of an ownership or role check are not in the contract: the API sends them with no body.
    if (response.status >= 400)
      return body === undefined ? [] : [`${label} ${response.status} has no body in the API, the mock sends one`];
    return [`${label} is mocked with ${response.status}; the API answers ${Object.keys(described).join(", ")}`];
  }
  const schema = responseSchema(spec);
  if (!schema)
    return body === undefined ? [] : [`${label} ${response.status} has no body in the API, the mock sends one`];
  if (body === undefined) return [`${label} ${response.status} has a body in the API, the mock sends none`];
  if (body instanceof NotJson) return notJson(`${label} ${response.status} mocked body`, body);
  return validate(schema, body).map((p) => `${label} ${response.status} mocked body: ${p}`);
}
