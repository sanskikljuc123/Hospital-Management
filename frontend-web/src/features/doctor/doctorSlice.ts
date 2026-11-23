import { createAsyncThunk, createSlice } from "@reduxjs/toolkit";
import { RootState } from "../../store/store";
import { Doctor } from "../../types/Dctor";
import api from "../../api/AxiosConfig";
import { apiErrorMessage } from "../../api/apiError";

interface DoctorsState {
  doctors: Doctor[];
  status: "idle" | "loading" | "failed";
  error?: string;
  count: number;
}

const initialState: DoctorsState = {
  doctors: [],
  status: "idle",
  count: 0,
};

export const fetchDoctors = createAsyncThunk<{ doctors: Doctor[] }, void, { rejectValue: string }>(
  "doctors/fetchDoctors",
  async (_, { rejectWithValue }) => {
    try {
      const response = await api.get("/doctor");
      return { doctors: response.data };
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const addDoctor = createAsyncThunk<Doctor, Partial<Doctor>, { state: RootState; rejectValue: string }>(
  "doctors/addDoctor",
  async (newDoctor, { rejectWithValue }) => {
    try {
      return (await api.post("/doctor", newDoctor)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const deleteDoctor = createAsyncThunk<string, string, { state: RootState; rejectValue: string }>(
  "doctors/deleteDoctor",
  async (doctorId, { rejectWithValue }) => {
    try {
      await api.delete(`/doctor/${doctorId}`);
      return doctorId;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

export const updateDoctor = createAsyncThunk<Doctor, Doctor, { state: RootState; rejectValue: string }>(
  "doctors/updateDoctor",
  async (updatedDoctor, { rejectWithValue }) => {
    const { id, ...newDoctor } = updatedDoctor;
    try {
      return (await api.put("/doctor/" + id, newDoctor)).data;
    } catch (error) {
      return rejectWithValue(apiErrorMessage(error));
    }
  },
);

const doctorSlice = createSlice({
  name: "doctors",
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchDoctors.pending, (state) => {
        state.status = "loading";
      })
      .addCase(fetchDoctors.fulfilled, (state, action) => {
        state.doctors = action.payload.doctors;
        state.status = "idle";
      })
      .addCase(fetchDoctors.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(addDoctor.pending, (state) => {
        state.status = "loading";
      })
      .addCase(addDoctor.fulfilled, (state, action) => {
        state.status = "idle";
        // check if the doctors is already in the list
        const index = state.doctors.findIndex((doctor) => doctor.id === action.payload.id);
        if (index == -1) {
          state.doctors.unshift(action.payload);
          state.count++;
        }
      })
      .addCase(addDoctor.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(deleteDoctor.pending, (state) => {
        state.status = "loading";
      })
      .addCase(deleteDoctor.fulfilled, (state, action) => {
        state.status = "idle";
        const index = state.doctors.findIndex((doctor) => doctor.id === parseInt(action.payload));
        if (index !== -1) {
          state.doctors.splice(index, 1);
          state.count--;
        }
      })
      .addCase(deleteDoctor.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      })
      .addCase(updateDoctor.pending, (state) => {
        state.status = "loading";
      })
      .addCase(updateDoctor.fulfilled, (state, action) => {
        state.status = "idle";
        const index = state.doctors.findIndex((doctor) => doctor.id === action.payload.id);
        if (index !== -1) {
          // check if the doctor is different from the one in the list
          if (JSON.stringify(state.doctors[index]) !== JSON.stringify(action.payload)) {
            state.doctors[index] = action.payload;
          }
        }
      })
      .addCase(updateDoctor.rejected, (state, action) => {
        state.status = "failed";
        state.error = action.payload ?? action.error.message;
      });
  },
});

export const getDoctorState = (state: RootState) => state.doctor;

export const selectStatus = (state: RootState) => state.doctor.status;

export const selectDoctors = (state: RootState) => state.doctor.doctors;

export const selectError = (state: RootState) => state.doctor.error;

export default doctorSlice.reducer;
