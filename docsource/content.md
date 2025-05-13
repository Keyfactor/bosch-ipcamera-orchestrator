## Overview

The Bosch IP Camera Orchestrator remotely manages certificates on the camera.

## Requirements

1. Out of the box, the camera comes with three accounts. You need an account created that has "service" level access:
![](docsource/images/Bosch_Security_Systems.gif)
2. Currently supports Bosch firmware version 7.10.0095 - 7.82. Has not been tested with any other firmeware version.

## Usage

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

![](docsource/images/reenrollment-example.png)

Running a Reenrollment job to issue a new certificate on the camera can happen in two ways. 
##### Manual Reenrollment Scheduling
Right click on the cert store and chooose Reenrollment. In the dialog box, type "SERIALNUMBER=xxxx,CN=Test" and click Done. A job will be created in the job queue that will perform on camera CSR that will be signed by a CA integrated with Keyfactor and then uploaded to the camera. Once complete, the camera will be rebooted. 
##### Automated Reenrollment Scheduling with Expiration Alerts
Start by installing the ExperationAlertHandler.ps1 on the Command server.

__Keyfactor Command before version 11__: copy the PowerShell to the ExtensionLibrary folder in the install location, typically `C:\Program Files\Keyfactor\ExtensionLibrary`

__Keyfactor Command version 11+__: upload the script using the API [documented here](https://software.keyfactor.com/Core-OnPrem/v11.5/Content/ReferenceGuide/PowerShellScripts.htm) so it can be used in an Expiration Alert Handler

After installing the PowerShell script, create a collection for each certificate type (or one for all cert types) used on cameras. Create an expiration alert and configure the Event Handler similar to the one below.
  
##### Event Handler Configuration 
Parameter Name	|Type           |Value
----------------|---------------|------------
DN	    |Token  |dn
Host    |Value  |FDDN of keyfactor server. Example: https://customer.keyfactor.com
Locations   |Token |locations:certstore
ScriptName  |Script |ExpirationAlertHandler.ps1

![](docsource/images/ExpirationAlerts.gif)