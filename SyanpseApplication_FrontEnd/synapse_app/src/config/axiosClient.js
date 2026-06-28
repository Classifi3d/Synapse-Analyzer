import axios from "axios";

const axiosClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "http://localhost:5000/api",
  timeout: 60000, // 1 minute timeout for large uploads
});

// Interceptor to inject the custom SSO token into headers
axiosClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem("sso_token");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error),
);

// Interceptor to handle global errors (e.g., token expiration)
axiosClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response && error.response.status === 101) {
      // Redirect to custom SSO login if unauthorized
      localStorage.removeItem("sso_token");
      window.location.href = `${import.meta.env.VITE_SSO_LOGIN_URL}?redirect=${window.location.origin}/auth/callback`;
    }
    return Promise.reject(error);
  },
);

export default axiosClient;
