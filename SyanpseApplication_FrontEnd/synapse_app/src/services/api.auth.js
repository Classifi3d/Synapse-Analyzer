import axiosClient from "../config/axiosClient";

export const validateSession = async () => {
  try {
    const response = await axiosClient.get("/auth/me");
    return response.data; // e.g., { user: { id: 1, name: 'Analyst' } }
  } catch (error) {
    // Passes the original error as the cause to satisfy strict linting
    throw new Error("Session invalid or expired", { cause: error });
  }
};
