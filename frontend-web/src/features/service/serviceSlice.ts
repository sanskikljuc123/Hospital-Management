import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { RootState } from "../../store/store";
import api from "../../api/AxiosConfig";
import { apiErrorMessage } from "../../api/apiError";
import { service } from "../../types/Service";

interface serviceState {
  services: service[];
  service: service;
  status: "idle" | "loading" | "failed";
  error?: string;
  count: number;
}

const initialState: serviceState = {
  services: [],
  status: "idle",
  count: 0,
  service: {
    id: 0,
    name: "",
    description: "",
  },
};

export const fetchServices = createAsyncThunk<service[], void, { rejectValue: string }>(
  "services/fetchServices",
  async (_, { rejectWithValue }) => {
    try {
      return (await api.get("/Service")).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const addService = createAsyncThunk<service, Partial<service>, { state: RootState; rejectValue: string }>(
  "services/addService",
  async (newService, { rejectWithValue }) => {
    try {
      return (await api.post("/service", newService)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const deleteService = createAsyncThunk<string, string, { state: RootState; rejectValue: string }>(
  "services/deleteService",
  async (serviceId, { rejectWithValue }) => {
    try {
      await api.delete(`/service/${serviceId}`);
      return serviceId;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const updateService = createAsyncThunk<service, service, { state: RootState; rejectValue: string }>(
  "services/updateService",
  async (updatedService, { rejectWithValue }) => {
    try {
      return (await api.put("/service/" + updatedService.id, updatedService)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const getServiceById = createAsyncThunk<service, number, { state: RootState; rejectValue: string }>(
  "services/getServiceById",
  async (id, { rejectWithValue }) => {
    try {
      return (await api.get("/service/" + id)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

const serviceSlice = createSlice({
  name: "service",
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchServices.pending, (state) => {
        state.status = "loading";
      })
      .addCase(fetchServices.fulfilled, (state, action) => {
        state.services = action.payload;
        state.status = "idle";
      })
      .addCase(fetchServices.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(addService.pending, (state) => {
        state.status = "loading";
      })
      .addCase(addService.fulfilled, (state, action) => {
        state.status = "idle";
        // check if the service is already in the list
        const index = state.services.findIndex((service) => service.id === action.payload.id);
        if (index == -1) {
          state.services.unshift(action.payload);
          state.count++;
        }
      })
      .addCase(addService.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(deleteService.pending, (state) => {
        state.status = "loading";
      })
      .addCase(deleteService.fulfilled, (state, action) => {
        state.status = "idle";
        const index = state.services.findIndex((service) => service.id === parseInt(action.payload));
        if (index !== -1) {
          state.services.splice(index, 1);
          state.count--;
        }
      })
      .addCase(deleteService.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(updateService.pending, (state) => {
        state.status = "loading";
      })
      .addCase(updateService.fulfilled, (state, action) => {
        state.status = "idle";

        const index = state.services.findIndex((service) => service.id === action.payload.id);
        if (index !== -1) {
          // check if the service is different from the one in the list
          if (JSON.stringify(state.services[index]) !== JSON.stringify(action.payload)) {
            state.services[index] = action.payload;
          }
        }
      })
      .addCase(updateService.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(getServiceById.pending, (state) => {
        state.status = "loading";
      })
      .addCase(getServiceById.fulfilled, (state, action) => {
        state.service = action.payload;
        state.status = "idle";
      })
      .addCase(getServiceById.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      });
  },
});

export const getserviceState = (state: RootState) => state.service;

export const selectStatus = (state: RootState) => state.service.status;

export const selectServices = (state: RootState) => state.service.services;

export const selectService = (state: RootState) => state.service.service;

export const selectError = (state: RootState) => state.service.error;

export default serviceSlice.reducer;
