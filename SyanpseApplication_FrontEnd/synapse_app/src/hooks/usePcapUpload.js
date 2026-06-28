// src/hooks/usePcapUpload.js
import { useMutation } from "@tanstack/react-query";
import { uploadPcapFile } from "../services/api.upload";

export const usePcapUpload = () => {
  return useMutation({
    mutationFn: uploadPcapFile,
    onError: (error) => {
      console.error("Failed to upload PCAP:", error);
    },
  });
};
