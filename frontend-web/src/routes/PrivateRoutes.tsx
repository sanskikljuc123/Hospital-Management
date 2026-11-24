import { Route, Routes, useLocation } from "react-router-dom";
import Service from "../pages/Service/Service";
import Home from "../pages/Home/Home";
import Doctor from "../pages/Doctor/Doctor";
import Patient from "../pages/Patient/Patient";
import Layout, { Content } from "antd/es/layout/layout";
import NavBar from "../components/NavBar/NavBar";
import SideMenu from "../components/SideMenu/SideMenu";
import { MdOutlineSpaceDashboard } from "react-icons/md";
import { FaUserDoctor } from "react-icons/fa6";
import { MdOutlineSick } from "react-icons/md";
import { RiServiceLine } from "react-icons/ri";
import { getDisplayName, logout } from "../auth/keycloak";

const PrivateRouts = () => {
  const location = useLocation();
  // The title follows the address on every render, so it cannot lag behind a navigation.
  const page = menuItems.find((item) => item.key === location.pathname);

  // Ends the Keycloak session as well, so the next visit asks for a login again.
  const handelLogOut = () => logout();
  return (
    <>
      <Layout style={{ overflow: "auto", display: "flex", flexDirection: "row", flexWrap: "nowrap", height: "100vh" }}>
        <SideMenu menuItems={menuItems} logOut={handelLogOut}></SideMenu>

        <Layout>
          <NavBar userName={getDisplayName()} pageName={page ? `${page.label} Page` : ""} userImg='' pageIcon={page?.icon} />
          <Content
            style={{
              margin: "24px 16px",
              padding: 24,
              minHeight: 280,
            }}
          >
            <Routes>
              <Route path='/' element={<Home />}></Route>
              <Route path='/home' element={<Home />}></Route>
              <Route path='/service' element={<Service />}></Route>
              <Route path='/doctor' element={<Doctor />}></Route>
              <Route path='/patient' element={<Patient />}></Route>
            </Routes>
          </Content>
        </Layout>
      </Layout>
    </>
  );
};
export default PrivateRouts;
const menuItems = [
  { key: "/home", label: "Home", icon: <MdOutlineSpaceDashboard /> },
  { key: "/doctor", label: "Doctors List", icon: <FaUserDoctor /> },
  { key: "/patient", label: "Patients List", icon: <MdOutlineSick /> },
  { key: "/service", label: "Services List", icon: <RiServiceLine /> },
];
