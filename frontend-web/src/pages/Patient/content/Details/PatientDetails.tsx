import React, { useEffect } from "react";
import { Patient } from "../../../../types/Patient";
import { useSelector } from "react-redux";
import { getServiceById, selectService } from "../../../../features/service/serviceSlice";
import { store } from "../../../../store/store";
interface PatientDetailsProps {
  patient: Patient;
}
const PatientDetails: React.FC<PatientDetailsProps> = ({ patient }) => {
  // Hooks run on every render, in the same order (rules of hooks; they were inside an if). The service is loaded
  // again when another patient is shown.
  const selected = useSelector(selectService);
  const serviceId = patient.serviceId;
  useEffect(() => {
    if (serviceId) store.dispatch(getServiceById(serviceId));
  }, [serviceId]);
  // Only the service of this patient: the store may still hold the one loaded for the patient shown before.
  const service = serviceId && selected.id === serviceId ? selected : patient.service;

  return (
    <>
      <p>
        <strong>Patient ID:</strong> {patient.id}
      </p>
      <p>
        <strong>Patient Name:</strong> {patient.userName}
      </p>

      {service && (
        <div>
          <h3>Service details:</h3>
          <p>
            <strong>Service ID:</strong> {service.id}
          </p>
          <p>
            <strong>Service Name:</strong> {service.name}
          </p>
          <p>
            <strong>Service Description:</strong> {service.description}
          </p>
        </div>
      )}
    </>
  );
};

export default PatientDetails;
