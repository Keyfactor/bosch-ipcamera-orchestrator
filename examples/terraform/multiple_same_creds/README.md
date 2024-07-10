# Multiple BoschIPCamera Store Creation using Same Credentials Example

This is an example of how to create multiple camera stores in Keyfactor Command using the Keyfactor Terraform provider.

## Pre-requisites

- Terraform is installed on the machine running the Terraform code
- The Keyfactor Terraform provider is installed and configured to communicate to Keyfactor Command. Review
  the [Keyfactor provider documentation](https://registry.terraform.io/providers/keyfactor-pub/keyfactor/latest/docs)
  for more information.
- The `BIPCamera` store type is already created in Keyfactor Command. See
  the [Extension specific documentation](https://github.com/Keyfactor/bosch-ipcamera-orchestrator?tab=readme-ov-file#store-type-configuration)
  for more information.
- An orchestrator with the BoschIPCamera extension is registered and approved in Keyfactor Command.

## Usage

Modify the `example.tfvars` file to include the necessary information for your environment. Alternatively Terraform will
prompt for each input if no value is provided.

*NOTE*: This example assumes all cameras are using the same credentials, if this does not suit your use-case then modify
accordingly.

```bash
terraform init
terraform plan
terraform apply
```

### Generate tfvars file from CSV

Alternatively, you can generate the `.tfvars` file from a CSV file using the template `example.csv` and running the
python script `csv2tfvars.py`. This script will generate a `.tfvars` based on the inputs of the CSV file.

#### Usage

```text
python csv2tfvars.py -h
usage: csv2tfvars.py [-h] [-csv CSV_FILE] [-u SERVER_USERNAME] [-p SERVER_PASSWORD] [-orch ORCHESTRATOR_NAME] [-i] [output_tfvars_file]

    Convert CSV to TFVARS. This script parses a given CSV file containing camera information and generates a Terraform variables file (.tfvars) with the data structured for Terraform usage.

    Usage:
        csv2tfvars.py -csv <input_csv_file> -orch <orchestrator_name> [output_tfvars_file] [-i]
        csv2tfvars.py --help

    The -i flag enables interactive mode, prompting for any missing required inputs.

positional arguments:
  output_tfvars_file    Output TFVARS file path. Optional, defaults to BoschIPCameraStores.tfvars.

optional arguments:
  -h, --help            show this help message and exit
  -csv CSV_FILE, --csv_file CSV_FILE
                        Path to the input CSV file. Required unless in interactive mode.
  -u SERVER_USERNAME, --server_username SERVER_USERNAME
                        Username for IP cameras. Required unless in interactive mode.
  -p SERVER_PASSWORD, --server_password SERVER_PASSWORD
                        Password for IP cameras. Required unless in interactive mode.
  -orch ORCHESTRATOR_NAME, --orchestrator_name ORCHESTRATOR_NAME
                        Orchestrator client name. Required unless in interactive mode.
  -i, --interactive     Run in interactive mode. Prompts for missing inputs.
```

#### Interactive Example

```bash
python csv2tfvars.py -i
```

```text
Enter the input CSV file path: example.csv
Enter the server username: admin
Enter the server password: admin
Enter the orchestrator_name: my-uo-client-name
Enter the output TFVARS file path (default is 'BoschIPCameraStores.tfvars'): 
TFVARS file generated: BoschIPCameraStores.tfvars

```

#### Non-Interactive Example

```bash
python csv2tfvars.py -csv example.csv -orch my-uo-client-name -u camera_username -p camera_passwd
```

<!-- BEGIN_TF_DOCS -->

## Requirements

| Name                                                                      | Version |
|---------------------------------------------------------------------------|---------|
| <a name="requirement_terraform"></a> [terraform](#requirement\_terraform) | >= 1.5  |
| <a name="requirement_keyfactor"></a> [keyfactor](#requirement\_keyfactor) | >=2.1.5 |

## Providers

| Name                                                                | Version |
|---------------------------------------------------------------------|---------|
| <a name="provider_keyfactor"></a> [keyfactor](#provider\_keyfactor) | 2.1.11  |

## Modules

No modules.

## Resources

| Name                                                                                                                                                      | Type        |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------|-------------|
| [keyfactor_certificate_store.bosch_camera_store](https://registry.terraform.io/providers/keyfactor-pub/keyfactor/latest/docs/resources/certificate_store) | resource    |
| [keyfactor_agent.universal_orchestrator](https://registry.terraform.io/providers/keyfactor-pub/keyfactor/latest/docs/data-sources/agent)                  | data source |

## Inputs

| Name                                                                                       | Description                                                                                                                                                            | Type          | Default | Required |
|--------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------|---------|:--------:|
| <a name="input_camera_map"></a> [camera\_map](#input\_camera\_map)                         | A map containing the serial number to IP address of the cameras to be enrolled                                                                                         | `map(string)` | n/a     |   yes    |
| <a name="input_inventory_schedule"></a> [inventory\_schedule](#input\_inventory\_schedule) | How often to update the inventory, valid options are number followed by 'm' for minutes, 'h' for hours, '1d' for daily, or 'immediate' for immediate inventory update. | `string`      | `"12h"` |    no    |
| <a name="input_orchestrator_name"></a> [orchestrator\_name](#input\_orchestrator\_name)    | The name or GUID of the orchestrator that has been registered and approved in Keyfactor Command                                                                        | `string`      | n/a     |   yes    |
| <a name="input_server_password"></a> [server\_password](#input\_server\_password)          | The password to authenticate to the Bosch camera                                                                                                                       | `string`      | n/a     |   yes    |
| <a name="input_server_use_ssl"></a> [server\_use\_ssl](#input\_server\_use\_ssl)           | Whether to use SSL when connecting to the Bosch camera                                                                                                                 | `bool`        | `true`  |    no    |
| <a name="input_server_username"></a> [server\_username](#input\_server\_username)          | The username to authenticate to the Bosch camera                                                                                                                       | `string`      | n/a     |   yes    |

## Outputs

No outputs.
<!-- END_TF_DOCS -->