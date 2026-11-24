import { useEffect, useState } from "react";
import { message } from "antd";
import DrawerComponent from "../../components/atoms/drawer/DrawerComponent";
import { store } from "../../store/store";
import PatientDetails from "./content/Details/PatientDetails";
import PatientForm from "./content/Form/PatientForm";
import Patients from "./template/Patients";
import { useSelector } from "react-redux";
import {
  addPatient,
  deletePatient,
  fetchPatients,
  selectPatients,
  selectStatus,
  updatePatient,
} from "../../features/patient/patientSlice";
import { Patient } from "../../types/Patient";

const PatientC: React.FC = () => {
  const patients = useSelector(selectPatients);
  const status = useSelector(selectStatus);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [patientDetails, setPatientDetails] = useState<Patient>();
  const [patientToUpdate, setPatientToUpdate] = useState<Patient>();
  const [drawerContent, setDrawerContent] = useState<"create" | "update" | "details">();
  const [drawerTitle, setDrawerTitle] = useState<string>("");
  useEffect(() => {
    // A list that cannot be loaded says so, instead of looking empty (UI_TEST_DESIGN D-6).
    store.dispatch(fetchPatients()).unwrap().catch((error: string) => message.error(error));
  }, []);

  return (
    <>
      <Patients
        patients={patients}
        status={status}
        handleFilterChange={(_filter) => {
          console.log("Filter Patient");
        }}
        handleEditPatient={(patient) => {
          setPatientToUpdate(patient);
          setDrawerContent("update");
          setDrawerTitle("Edit patient");
          setIsDrawerOpen(true);
        }}
        handleDeletePatient={(id) => {
          // The row goes only when the API confirms; a refusal (e.g. 409, still in use) is shown (D-6).
          store
            .dispatch(deletePatient(String(id)))
            .unwrap()
            .then(() => message.success("Patient deleted"))
            .catch((error: string) => message.error(error));
        }}
        handleViewPatient={(patient) => {
          setPatientDetails(patient);
          setDrawerContent("details");
          setDrawerTitle("patient Details");
          setIsDrawerOpen(true);
        }}
        handleCreatePatient={() => {
          setDrawerContent("create");
          setDrawerTitle("Create patient");
          setIsDrawerOpen(true);
        }}
      />
      <DrawerComponent
        isOpen={isDrawerOpen}
        content={
          drawerContent === "create" || drawerContent === "update" ? (
            <PatientForm
              onSubmit={async (patient) => {
                // The drawer closes only when the API saved the patient; otherwise it stays open with the
                // user's input and shows the API's reason (UI_TEST_DESIGN D-6).
                try {
                  if (drawerContent === "create") await store.dispatch(addPatient(patient)).unwrap();
                  else if (drawerContent === "update") await store.dispatch(updatePatient(patient)).unwrap();
                  message.success(drawerContent === "create" ? "Patient created" : "Patient saved");
                  setIsDrawerOpen(false);
                } catch (error) {
                  message.error(error as string);
                }
              }}
              patient={drawerContent === "update" ? patientToUpdate : undefined}
            />
          ) : (
            <PatientDetails patient={patientDetails!} />
          )
        }
        handleClose={() => setIsDrawerOpen(false)}
        title={drawerTitle}
      />
    </>
  );
};
export default PatientC;
