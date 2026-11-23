import { useEffect, useState } from "react";
import { message } from "antd";
import DrawerComponent from "../../components/atoms/drawer/DrawerComponent";
import DoctorDetails from "./content/Details/DoctorDetails";
import DoctorForm from "./content/Form/DoctorForm";
import { useSelector } from "react-redux";
import { Doctor } from "../../types/Dctor";
import { store } from "../../store/store";
import {
  addDoctor,
  deleteDoctor,
  fetchDoctors,
  selectDoctors,
  selectStatus,
  updateDoctor,
} from "../../features/doctor/doctorSlice";
import Doctors from "./template/Doctors";

const Doctorc: React.FC = () => {
  const doctors = useSelector(selectDoctors);
  const status = useSelector(selectStatus);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [doctorDetails, setDoctorDetails] = useState<Doctor>();
  const [doctorToUpdate, setDoctorToUpdate] = useState<Doctor>();
  const [drawerContent, setDrawerContent] = useState<"create" | "update" | "details">();
  const [drawerTitle, setDrawerTitle] = useState<string>("");
  useEffect(() => {
    // A list that cannot be loaded says so, instead of looking empty (UI_TEST_DESIGN D-6).
    store.dispatch(fetchDoctors()).unwrap().catch((error: string) => message.error(error));
  }, []);

  return (
    <>
      <Doctors
        doctors={doctors}
        status={status}
        handleFilterChange={(_filter) => {
          console.log("Filter doctor");
        }}
        handleEditDoctor={(doctor) => {
          setDoctorToUpdate(doctor);
          setDrawerContent("update");
          setDrawerTitle("Edit doctor");
          setIsDrawerOpen(true);
        }}
        handleDeleteDoctor={(id) => {
          // The row goes only when the API confirms; a refusal (e.g. 409, still in use) is shown (D-6).
          store
            .dispatch(deleteDoctor(id))
            .unwrap()
            .then(() => message.success("Doctor deleted"))
            .catch((error: string) => message.error(error));
        }}
        handleViewDoctor={(doctor) => {
          setDoctorDetails(doctor);
          setDrawerContent("details");
          setDrawerTitle("Doctor Details");
          setIsDrawerOpen(true);
        }}
        handleCreateDoctor={() => {
          setDrawerContent("create");
          setDrawerTitle("Create doctor");
          setIsDrawerOpen(true);
        }}
      />
      <DrawerComponent
        isOpen={isDrawerOpen}
        content={
          drawerContent === "create" || drawerContent === "update" ? (
            <DoctorForm
              onSubmit={async (doctor) => {
                // The drawer closes only when the API saved the doctor; otherwise it stays open with the
                // user's input and shows the API's reason (UI_TEST_DESIGN D-6).
                try {
                  if (drawerContent === "create") await store.dispatch(addDoctor(doctor)).unwrap();
                  else if (drawerContent === "update") await store.dispatch(updateDoctor(doctor)).unwrap();
                  message.success(drawerContent === "create" ? "Doctor created" : "Doctor saved");
                  setIsDrawerOpen(false);
                } catch (error) {
                  message.error(error as string);
                }
              }}
              doctor={drawerContent === "update" ? doctorToUpdate : undefined}
            />
          ) : (
            <DoctorDetails doctor={doctorDetails!} />
          )
        }
        handleClose={() => setIsDrawerOpen(false)}
        title={drawerTitle}
      />
    </>
  );
};
export default Doctorc;
