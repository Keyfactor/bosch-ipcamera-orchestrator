
# Bosch IP Camera

Bosch IP Camera Orchestrator for Inventory and Reenrollment (on-device keygen) for existing and new certificates

#### Integration status: Production - Ready for use in production environments.

## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.

## Support for Bosch IP Camera

Bosch IP Camera is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

---


---



## Keyfactor Version Supported

The minimum version of the Keyfactor Universal Orchestrator Framework needed to run this version of the extension is 10.1
## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|  |  |
|Supports Management Remove|  |  |
|Supports Create Store|  |  |
|Supports Discovery|  |  |
|Supports Reenrollment|&check; |  |
|Supports Inventory|&check; |  |





---


**Bosch Camera Configuration**

**Overview**

The Bosch IP Camera Orchestrator remotely manages certificates on the camera.

This agent implements two job types – Inventory and Reenrollment. Below are the steps necessary to configure Universal Orchestrator extension.

**Bosch IP Camera Configuration**

1. Out of the box, the camera comes with three accounts. You need an account created that has "service" level access:
![](images/Bosch_Security_Systems.gif)
2. Currently supports Bosch firmware version 7.10.0095 - 7.82. Has not been tested with any other firmeware version.

**1. Create the New Certificate Store Type for the Bosch IP Camera Universal Orchestrator**

In Keyfactor Command create a new Certificate Store Type similar to the one below:

#### STORE TYPE CONFIGURATION
| Setting Tab | Config Element | Value |
|-|-|-|
| Basic | Name | Bosch IP Camera |
| Basic | Short Name | BIPCamera |
| Basic | Custom Capability | &check; value: `BoschIpCamera` |
| Basic | Job Types	| Inventory, Reenrollment |
| Basic | Needs Server | &check; |
| Advanced | Store Path Type | Freeform |
| Advanced | Supports Custom Alias | Required |
| Advanced | Private Key Handling | Optional |
| Advanced | PFX Password Style | Default |
| Entry Parameters| See Below | See Below |

**Basic Settings:**

![](images/store-type-basic.png)

**Advanced Settings:**

![](images/store-type-advanced.png)

**Entry Params:**

![](images/store-type-entry.png)

The entry parameters that need to be created are as follows:
| Name | Display Name | Type | Default Value | Multiple Choice Options | Required When |
|-|-|-|-|-|-|
| CertificateUsage | Certificate Usage | Multiple Choice | | `,HTTPS,EAP-TLS-client,TLS-DATE-client` | none selected |
| Name | Name (Alias) | String | | N/A | &check; Reenrolling an entry |
| Overwrite | Overwrite | Bool | False | N/A | none selected |

![](images/entry-certificate-usage.png)
![](images/entry-name.png)
![](images/entry-overwrite.png)

**2. Register the BoschIPCamera Universal Orchestrator with Keyfactor**

Within Windows File Explorer, navigate to the Keyfactor Orchestrator installation folder (usually C:\Program Files\Keyfactor\Keyfactor Orchestrator), find the "extensions" folder, and under that create a new folder named "BoschIPCamera". Under the BoschIPCamera folder copy all of the files from the downloaded release to this location.

**3. Create a Bosch IP Camera Store within Keyfactor Command**

In Keyfactor Command create a new Certificate Store setting the values based on the camera settings below:

#### STORE CONFIGURATION 
| Config Element | Description |
|-|-|
| Category |The type of certificate store to be configured. Select category based on the display name configured above "Bosch IP Camera". |
| Container | This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store. |
| Client Machine | The IP address of the Camera.  Sample is "192.167.231.174:44444". Include the port if necessary. |
| Store Path | Enter the Serial Number of the camera e.g. `068745431065110085` |
| Orchestrator | This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. |
| Server Username | Enter the username of the configured "service" user on the camera |
| Server Password | Enter the password of the configured "service" user on the camera |
| Use SSL | Select True or False depending on if SSL (HTTPS) should be used to communicate with the camera. |
| Inventory Schedule | The interval that the system will use to report on what certificates are currently in the store. |


#### Usage

**Reenrollment**

**Important!** When using Reenrollment, the subject needs to include the Camera's serial number as an element. The Camera automatically adds this to the CSR it generates, and Keyfactor will not enroll the CSR unless it is included.
For example, with a serial number of '1234' and a desired subject of CN=mycert, the Subject entered for a reenrollment should read:
Subject:  `SERIALNUMBER=1234,CN=mycert`
The serial number is entered as the Store Path on the Certificate Store, and should be copied and entered as mentioned when running a reenrollment job.

| Reenrollment Field | Value | Description |
|-|-|-|
| Subject Name | `SERIALNUMBER=xxxx,CN=mycert,O=...` etc. | Comma-separated list of subject elements. Must include `SERIALNUMBER=` as described above. |
| Alias | Alias | The certificate Alias, and name to be assigned on the camera. Will allow for overwriting existing certs with the same name. |
| Certificate Usage | Select one, or blank | The Certificate Usage to assign to the cert after upload. Can be left blank to be assigned later. |
| Name (Alias) | Alias | The certificate Alias, entered again. |
| Overwrite | True, or False | Select `True` if using an existing Alias name to remove and replace an existing certificate. |

![](images/reenrollment-example.png)

Running a Reenrollment job to issue a new certificate on the camera can happen in two ways. 
##### Manual Reenrollment Scheduling
Right click on the cert store and chooose Reenrollment. In the dialog box, type "SERIALNUMBER=xxxx,CN=Test" and click Done. A job will be created in the job queue that will perform on camera CSR that will be signed by a CA integrated with Keyfactor and then uploaded to the camera. Once complete, the camera will be rebooted. 
##### Automated Reenrollment Scheduling with Expiration Alerts

__Keyfactor Command before version 11__: copy the PowerShell to the ExtensionLibrary folder in the install location, typically `C:\Program Files\Keyfactor\ExtensionLibrary`

__Keyfactor Command version 11+__: upload the script using the API [documented here](https://software.keyfactor.com/Core-OnPrem/v11.5/Content/ReferenceGuide/PowerShellScripts.htm) so it can be used in an Expiration Alert Handler

A sample script is provided to get started with configuring an `ExpirationPowershell` alert. It needs to be updated with the correct URL for API requests, and may need other changes as well, as it assumes that Default Credentials (Windows Auth) can be used to authenticate API requests to the Keyfactor instance.

After installing the PowerShell script, create a collection for each certificate type (or one for all cert types) used on cameras. The `ExpirationPowershell` Event Handler configuration should be configured with the following values:
  
##### Event Handler Configuration 
| Parameter Name | Type | Value |
| - | - | - |
| Thumbprint | Special Text | Thumbprint |
| Template | Renewal Template | `desired renewal template` |
| CAConfiguration | Renewal Certificate Authority | `desired renewal CA` |
| ScriptName | PowerShell Script Name | BoschIPCameraExpirationHandler.ps1 |

When running the sample script, it will assume that all certs passed to the script should schedule a Reenrollment job with their existing parameters, and force an Overwrite of the existing cert.
If the job should fail to upload a new certificate, the existing certificate may have been deleted and there may no longer be a valid certificate available when using Overwrite.


When creating cert store type manually, that store property names and entry parameter names are case sensitive


