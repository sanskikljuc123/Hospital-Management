import { useSelector } from "react-redux";
import Services from "./template/Services";
import {
  addService,
  deleteService,
  fetchServices,
  selectServices,
  selectStatus,
  updateService,
} from "../../features/service/serviceSlice";
import { store } from "../../store/store";
import { useEffect, useState } from "react";
import { message } from "antd";
import ServiceDetails from "./content/Details/ServiceDetails";
import { service } from "../../types/Service";
import ServiceForm from "./content/Form/ServiceForm";
import DrawerComponent from "../../components/atoms/drawer/DrawerComponent";

const Service: React.FC = () => {
  const services = useSelector(selectServices);
  const status = useSelector(selectStatus);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [serviceDetails, setServiceDetails] = useState<service>();
  const [serviceToUpdate, setServiceToUpdate] = useState<service>();
  const [drawerContent, setDrawerContent] = useState<"create" | "update" | "details">();
  const [drawerTitle, setDrawerTitle] = useState<string>("");
  useEffect(() => {
    // A list that cannot be loaded says so, instead of looking empty (UI_TEST_DESIGN D-6).
    store
      .dispatch(fetchServices())
      .unwrap()
      .catch((error: string) => message.error(error));
  }, []);

  return (
    <>
      <Services
        services={services}
        status={status}
        handleFilterChange={(_filter) => {
          console.log("Filter Service");
        }}
        handleEditService={(service) => {
          setServiceToUpdate(service);
          setDrawerContent("update");
          setDrawerTitle("Edit Service");
          setIsDrawerOpen(true);
        }}
        handleDeleteService={(id) => {
          // The row goes only when the API confirms; a refusal (e.g. 409, still in use) is shown (D-6).
          store
            .dispatch(deleteService(String(id)))
            .unwrap()
            .then(() => message.success("Service deleted"))
            .catch((error: string) => message.error(error));
        }}
        handleViewService={(service) => {
          setServiceDetails(service);
          setDrawerContent("details");
          setDrawerTitle("Service Details");
          setIsDrawerOpen(true);
        }}
        handleCreateService={() => {
          setDrawerContent("create");
          setDrawerTitle("Create Service");
          setIsDrawerOpen(true);
        }}
      />
      <DrawerComponent
        isOpen={isDrawerOpen}
        content={
          drawerContent === "create" || drawerContent === "update" ? (
            <ServiceForm
              onSubmit={async (service) => {
                // The drawer closes only when the API saved the service; otherwise it stays open with the
                // user's input and shows the API's reason (UI_TEST_DESIGN D-6).
                try {
                  if (drawerContent === "create") await store.dispatch(addService(service)).unwrap();
                  else if (drawerContent === "update") await store.dispatch(updateService(service)).unwrap();
                  message.success(drawerContent === "create" ? "Service created" : "Service saved");
                  setIsDrawerOpen(false);
                } catch (error) {
                  message.error(error as string);
                }
              }}
              service={drawerContent === "update" ? serviceToUpdate : undefined}
            />
          ) : (
            <ServiceDetails service={serviceDetails!} />
          )
        }
        handleClose={() => setIsDrawerOpen(false)}
        title={drawerTitle}
      />
    </>
  );
};
export default Service;
