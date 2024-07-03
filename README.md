<h1 align="center" style="border-bottom: none">
    Bosch IP Camera Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/bosch-ipcamera-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/bosch-ipcamera-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/bosch-ipcamera-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/bosch-ipcamera-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>


## Overview

The Bosch IP Camera Universal Orchestrator extension facilitates the remote management of cryptographic certificates for Bosch IP Cameras via Keyfactor Command. Bosch IP Cameras use certificates to secure communications, including HTTPS, EAP-TLS client authentication, and TLS-DATE client authentication.

A defined Certificate Store for the Bosch IP Camera Store Type represents a collection of certificates managed on a particular Bosch IP Camera. These Certificate Stores allow administrators to perform inventory operations, add or remove certificates, and manage certificate lifecycles effectively on remote cameras. By leveraging the orchestration capabilities of Keyfactor Command, this Universal Orchestrator extension ensures that all certificates remain up-to-date and compliant with security policies.

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The Bosch IP Camera Universal Orchestrator extension is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Installation
Before installing the Bosch IP Camera Universal Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


1. Follow the [requirements section](docs/bipcamera.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

    No requirements found



    </details>

2. Create Certificate Store Types for the Bosch IP Camera Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # Bosch IP Camera
        kfutil store-types create BIPCamera
        ```

    * **Manually**:
        * [Bosch IP Camera](docs/bipcamera.md#certificate-store-type-configuration)

3. Install the Bosch IP Camera Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e bosch-ipcamera-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e bosch-ipcamera-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Bosch IP Camera Universal Orchestrator extension](https://github.com/Keyfactor/bosch-ipcamera-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.

    * [Bosch IP Camera](docs/bipcamera.md#certificate-store-configuration)



## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).