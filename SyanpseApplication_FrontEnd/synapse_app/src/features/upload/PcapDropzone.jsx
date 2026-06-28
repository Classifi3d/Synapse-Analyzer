// src/features/upload/PcapDropzone.jsx
import { useCallback } from "react";
import { useDropzone } from "react-dropzone";
import { If, Then } from "react-if";

const PcapDropzone = ({ onFileAccept }) => {
  const onDrop = useCallback(
    (acceptedFiles) => {
      if (acceptedFiles && acceptedFiles.length > 0) {
        // Pass the valid file instance back up to the main AnalysisBoard
        onFileAccept(acceptedFiles[0]);
      }
    },
    [onFileAccept],
  );

  const {
    getRootProps,
    getInputProps,
    isDragActive,
    isDragReject,
    fileRejections,
  } = useDropzone({
    onDrop,
    multiple: false, // Ensure only one network trace is analyzed at a time
    accept: {
      "application/vnd.tcpdump.pcap": [".pcap"],
      "application/x-pcapng": [".pcapng"],
      "application/octet-stream": [".pcap", ".pcapng"], // Fallback for OS-level generic binaries
    },
  });

  // Dynamically determine CSS classes based on drag-and-drop state
  const getContainerClass = () => {
    let baseClass =
      "d-flex flex-column align-items-center justify-content-center p-4 rounded-top border text-center transition-all cursor-pointer ";
    if (isDragReject)
      return baseClass + "border-danger bg-danger bg-opacity-10 text-danger";
    if (isDragActive)
      return baseClass + "border-primary bg-primary bg-opacity-10 text-primary";
    return baseClass + "border-secondary bg-dark text-muted border-dashed";
  };

  return (
    <div className="w-100 position-relative">
      <div
        {...getRootProps({ className: getContainerClass() })}
        style={{ borderStyle: "dashed", borderWidth: "2px", cursor: "pointer" }}
      >
        <input {...getInputProps()} />

        <i
          className={`bi ${isDragActive ? "bi-cloud-arrow-up-fill" : "bi-file-earmark-binary"} fs-3 mb-2`}
        ></i>

        <p className="mb-0 small">
          <If condition={isDragActive && !isDragReject}>
            <Then>Drop the packet capture here...</Then>
          </If>
          <If condition={isDragReject}>
            <Then>Unsupported file type. Please use .pcap or .pcapng</Then>
          </If>
          <If condition={!isDragActive}>
            <Then>
              <span>
                Drag & drop a <strong>.pcap</strong> or <strong>.pcapng</strong>{" "}
                file, or{" "}
              </span>
              <span className="text-primary text-decoration-underline">
                browse
              </span>
            </Then>
          </If>
        </p>
      </div>

      {/* Clean handling of validation errors without relying on state side-effects */}
      <If condition={fileRejections.length > 0}>
        <Then>
          <div className="text-danger small mt-1 position-absolute start-0 px-2 fw-semibold">
            Invalid file configuration. Ensure the file extension is strictly
            .pcap or .pcapng
          </div>
        </Then>
      </If>
    </div>
  );
};

export default PcapDropzone;
