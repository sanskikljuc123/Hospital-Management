import { Button, Popconfirm, Space, Table, TableProps, Tooltip } from "antd";
import Input, { SearchProps } from "antd/es/input";
import { useEffect, useState } from "react";
import { ServicesProps } from "./ServicesProps";
import { service } from "../../../types/Service";
import Title from "antd/es/typography/Title";
import { HiOutlineEye, HiOutlinePencilAlt, HiOutlinePlus, HiOutlineTrash } from "react-icons/hi";
import "./Services.scss";

const Services: React.FC<ServicesProps> = ({
  services,
  status,
  handleViewService,
  handleEditService,
  handleDeleteService,
  handleCreateService,
}: ServicesProps) => {
  const { Search } = Input;
  const onSearch: SearchProps["onSearch"] = (value, _e, info) => console.log(info?.source, value);

  const [tableContent, setTableContent] = useState<service[]>([]);
  const [countServices, setcountServices] = useState(tableContent.length);
  useEffect(() => {
    // Keep the whole record: Edit and View receive the row, and a field dropped here would be missing from
    // the edit form and cleared by the API's PUT (e.g. departmentId). The columns decide what is shown.
    const tableContent = services.map((service) => ({ ...service }));
    setTableContent(tableContent);
    setcountServices(tableContent.length);
  }, [services]);

  const columns: TableProps<service>["columns"] = [
    {
      title: "ID",
      dataIndex: "id",
      key: "id",
      width: 50,
    },
    {
      title: "Service Name",
      dataIndex: "name",
      key: "name",
      render: (text) => <div style={{ display: "flex", gap: "6px" }}>{text}</div>,
    },
    {
      title: "Description",
      dataIndex: "description",
      key: "description",
    },
    {
      title: "Action",
      key: "action",
      width: 160,
      render: (_text, record) => (
        <Space size='middle'>
          <Tooltip title='View'>
            <Button aria-label='View' onClick={() => handleViewService(record)} className='table--action-btn' icon={<HiOutlineEye />} />
          </Tooltip>
          <Tooltip title='Edit'>
            <Button
              aria-label='Edit'
              onClick={() => handleEditService(record)}
              className='table--action-btn'
              icon={<HiOutlinePencilAlt />}
            />
          </Tooltip>
          <Tooltip title='Delete'>
            <Popconfirm title='Sure to delete?' onConfirm={() => handleDeleteService(record.id)}>
              <Button aria-label='Delete' className='table--action-btn' icon={<HiOutlineTrash />} />
            </Popconfirm>
          </Tooltip>
        </Space>
      ),
    },
  ];

  return (
    <div className='service-container'>
      <div className='service-container--header'>
        <Title level={4}> Total Services number: {countServices}</Title>
        <div className='service-container--header-actions'>
          <Search
            className='service-container--header-actions-search'
            placeholder='input search text'
            allowClear
            onSearch={onSearch}
            style={{ width: 200 }}
          />
          <Button
            onClick={() => {
              handleCreateService();
            }}
            type='primary'
            icon={<HiOutlinePlus />}
          >
            Add a new service
          </Button>
        </div>
      </div>
      <div className='service-container--table'>
        <Table
          loading={status == "loading"}
          columns={columns}
          dataSource={tableContent}
          rowKey='id'
          pagination={false}
          scroll={{ y: 380 }}
        />
      </div>
    </div>
  );
};
export default Services;
