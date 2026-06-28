export const fetchLlmStream = async (prompt, fileId) => {
  const token = localStorage.getItem("sso_token");

  const response = await fetch(
    `${import.meta.env.VITE_API_BASE_URL}/analyze/stream`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ prompt, fileId }),
    },
  );

  if (!response.ok) {
    throw new Error("Network response was not ok");
  }

  if (!response.body) {
    throw new Error("ReadableStream not supported by this browser.");
  }

  return response.body;
};
