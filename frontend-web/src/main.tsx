import React from "react";
import { BrowserRouter } from "react-router-dom";
import ReactDOM from "react-dom/client";
import App from "./App.tsx";
import "./index.scss";
import { Provider } from "react-redux";
import { store } from "./store/store.ts";
import "./styles/typography/headings.css";
import "./styles/typography/text.css";
import "./styles/colors/colors.css";
import AuthGate from "./auth/AuthGate.tsx";
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AuthGate>
      <Provider store={store}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </Provider>
    </AuthGate>
  </React.StrictMode>,
);
