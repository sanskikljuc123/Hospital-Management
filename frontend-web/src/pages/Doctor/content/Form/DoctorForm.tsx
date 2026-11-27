import React, { useEffect } from "react";
import { Form, Input, Button, Select, DatePicker } from "antd";
import dayjs, { Dayjs } from "dayjs";
import { store } from "../../../../store/store";
import { fetchServices, selectServices } from "../../../../features/service/serviceSlice";
import { useSelector } from "react-redux";
import { Doctor } from "../../../../types/Dctor";

interface DoctorFormProps {
  doctor?: Doctor;
  onSubmit: (values: Doctor) => void;
}

const DoctorForm: React.FC<DoctorFormProps> = ({ doctor, onSubmit }) => {
  const [form] = Form.useForm();
  const services = useSelector(selectServices);
  useEffect(() => {
    store.dispatch(fetchServices());
  }, []);

  // The API stores the birthday as a calendar date. Read only the date part ("1980-01-02") and show it as
  // that local day, so the picker never moves it to the previous day in a time zone west of UTC.
  const initialValues = doctor
    ? { ...doctor, birthday: doctor.birthday ? dayjs(doctor.birthday.slice(0, 10)) : undefined }
    : {};
  // Send the birthday as "YYYY-MM-DD", not as a UTC timestamp: local midnight in UTC+1 is the day before
  // in UTC and would be saved one day early.
  const handleSubmit = async (values: Omit<Doctor, "birthday"> & { birthday?: Dayjs }) => {
    onSubmit({
      ...values,
      birthday: values.birthday ? values.birthday.format("YYYY-MM-DD") : null,
    } as unknown as Doctor);
  };

  return (
    <Form form={form} initialValues={initialValues} onFinish={handleSubmit} layout='vertical'>
      {/* Hidden input field for id */}
      {doctor && (
        <Form.Item name='id' style={{ display: "none" }}>
          <Input type='hidden' />
        </Form.Item>
      )}
      {/* The form has no department picker yet. Sending the current department keeps it: the API's PUT
          replaces every field, and a missing one would be cleared. */}
      {doctor && (
        <Form.Item name='departmentId' style={{ display: "none" }}>
          <Input type='hidden' />
        </Form.Item>
      )}
      <Form.Item
        label='Doctor First Name'
        name='firstName'
        rules={[{ required: true, message: "Please enter the doctor name" }]}
      >
        <Input />
      </Form.Item>
      <Form.Item
        label='Doctor Last Name'
        name='lastName'
        rules={[{ required: true, message: "Please enter the doctor name" }]}
      >
        <Input />
      </Form.Item>
      <Form.Item
        label='Doctor Address'
        name='address'
        rules={[{ required: true, message: "Please enter the doctor address" }]}
      >
        <Input />
      </Form.Item>
      {/* Shown when editing too: otherwise the birthday is not sent and PUT clears it. Required only for
          new doctors, so a doctor stored without a birthday can still be edited. */}
      <Form.Item
        label='Doctor Birthday'
        name='birthday'
        rules={[{ required: !doctor, message: "Please enter the doctor birthday" }]}
      >
        <DatePicker format='YYYY-MM-DD' />
      </Form.Item>

      <Form.Item
        label='Select Service'
        name='serviceIds'
        rules={[{ required: true, message: "Please select services" }]}
      >
        <Select mode='multiple'>
          {services.map((service) => (
            <Select.Option key={service.id} value={service.id}>
              {service.name}
            </Select.Option>
          ))}
        </Select>
      </Form.Item>
      <Form.Item>
        <Button type='primary' htmlType='submit'>
          {doctor ? "Update Doctor" : "Create Doctor"}
        </Button>
      </Form.Item>
    </Form>
  );
};

export default DoctorForm;
