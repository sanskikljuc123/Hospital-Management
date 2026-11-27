import { configureStore } from "@reduxjs/toolkit";
import doctorSlice from "../features/doctor/doctorSlice";
import patientSlice from "../features/patient/patientSlice";
import serviceSlice from "../features/service/serviceSlice";

// Exported so the component tests reset the store with the same reducers the app uses (src/test/setup.ts, R-071).
export const reducer = {
  doctor: doctorSlice,
  patient: patientSlice,
  service: serviceSlice,
};

export const store = configureStore({ reducer });

// Infer the `RootState` and `AppDispatch` types from the store itself
export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
