
# BmsTelemetry

BmsTelemetry is a application designed to poll Building Management System (BMS) devices, collect telemetry, and securely upload the data to Azure IoT Hub using Device Provisioning Service.  
It includes a lightweight Blazor-based web interface for monitoring, configuration visibility, and troubleshooting.

---

## Features

- Polls multiple BMS device types:
  - `Danfoss`
  - `EmersonE2`
  - `EmersonE3`
- Device communication over HTTP/IP with retry, timeout, and keep-alive policies
- Azure IoT Hub integration using DPS and Key Vault–stored credentials
- Background service architecture
- Built-in logging with file rotation
- Web frontend served via Kestrel for local or remote access
- Fully configurable via `appsettings.json`

---

## Requirements

- **.NET 10 SDK or Runtime**
- **GNU make**
- (Optional) Azure IoT Hub + DPS + Key Vault for cloud telemetry

---

## Running the Application

### Run native
```bash
make run
```

### Publish Windows EXE
```bash
make winpub
```

### Implementation details
See BmsTelemetry/HELP.md for details
