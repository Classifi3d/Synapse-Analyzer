// src/services/api.upload.js
import axiosClient from "../config/axiosClient";

export const uploadPcapFile = async ({ file, onProgress }) => {
  const formData = new FormData();
  formData.append("file", file);

  // Note: For multi-gigabyte files, this is where you would implement
  // a chunked upload loop (slicing the file and sending sequentially).
  // This example uses standard multipart form data for typical captures.
  const response = await axiosClient.post("/upload/pcap", formData, {
    headers: {
      "Content-Type": "multipart/form-data",
    },
    onUploadProgress: (progressEvent) => {
      if (onProgress && progressEvent.total) {
        const percentCompleted = Math.round(
          (progressEvent.loaded * 100) / progressEvent.total,
        );
        onProgress(percentCompleted);
      }
    },
  });

  return response.data; // e.g., returns { fileId: '123-abc' }
};
