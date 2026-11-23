import { Patient } from "./../../types/Patient";

import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { RootState } from "../../store/store";
import api from "../../api/AxiosConfig";
import { apiErrorMessage } from "../../api/apiError";

interface PatientState {
  patients: Patient[];
  status: "idle" | "loading" | "failed";
  error?: string;
  count: number;
}

const initialState: PatientState = {
  patients: [],
  status: "idle",
  count: 0,
};

export const fetchPatients = createAsyncThunk<{ patient: Patient[] }, void, { rejectValue: string }>(
  "patients/fetchPatients",
  async (_, { rejectWithValue }) => {
    try {
      const response = await api.get("/Patient");
      return { patient: response.data };
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const addPatient = createAsyncThunk<Patient, Partial<Patient>, { state: RootState; rejectValue: string }>(
  "patients/addPatient",
  async (newPatient, { rejectWithValue }) => {
    try {
      return (await api.post("/Patient", newPatient)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const deletePatient = createAsyncThunk<string, string, { state: RootState; rejectValue: string }>(
  "patients/deletePatient",
  async (PatientId, { rejectWithValue }) => {
    try {
      await api.delete(`/Patient/${PatientId}`);
      return PatientId;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const updatePatient = createAsyncThunk<Patient, Patient, { state: RootState; rejectValue: string }>(
  "patients/updatePatient",
  async (updatedPatient, { rejectWithValue }) => {
    try {
      // The API returns the updated patient (PatientDto).
      return (await api.put("/Patient/" + updatedPatient.id, updatedPatient)).data as Patient;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

const patientSlice = createSlice({
  name: "patient",
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchPatients.pending, (state) => {
        state.status = "loading";
      })
      .addCase(fetchPatients.fulfilled, (state, action) => {
        state.patients = action.payload.patient;
        state.status = "idle";
      })
      .addCase(fetchPatients.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(addPatient.pending, (state) => {
        state.status = "loading";
      })
      .addCase(addPatient.fulfilled, (state, action) => {
        state.status = "idle";
        // check if the patient is already in the list
        const index = state.patients.findIndex((Patient) => Patient.id === action.payload.id);
        if (index == -1) {
          state.patients.unshift(action.payload);
          state.count++;
        }
      })
      .addCase(addPatient.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(deletePatient.pending, (state) => {
        state.status = "loading";
      })
      .addCase(deletePatient.fulfilled, (state, action) => {
        state.status = "idle";
        const index = state.patients.findIndex((Patient) => Patient.id === parseInt(action.payload));
        if (index !== -1) {
          state.patients.splice(index, 1);
          state.count--;
        }
      })
      .addCase(deletePatient.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(updatePatient.pending, (state) => {
        state.status = "loading";
      })
      .addCase(updatePatient.fulfilled, (state, action) => {
        state.status = "idle";
        const index = state.patients.findIndex((Patient) => Patient.id === action.payload.id);
        if (index !== -1) {
          // check if the Patient is different from the one in the list
          if (JSON.stringify(state.patients[index]) !== JSON.stringify(action.payload.id)) {
            state.patients[index] = action.payload;
          }
        }
      })
      .addCase(updatePatient.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      });
  },
});

export const getPatientState = (state: RootState) => state.patient;

export const selectStatus = (state: RootState) => state.patient.status;

export const selectPatients = (state: RootState) => state.patient.patients;

export const selectError = (state: RootState) => state.patient.error;

export default patientSlice.reducer;
