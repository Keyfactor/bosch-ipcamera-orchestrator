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

| Name                                                                                               | Description                                                                                                                                                            | Type     | Default | Required |
|----------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|---------|:--------:|
| <a name="input_camera_ip"></a> [camera\_ip](#input\_camera\_ip)                                    | The IP address of the Camera. Example: `192.167.231.174:44444`. Include the port if necessary.                                                                         | `string` | n/a     |   yes    |
| <a name="input_camera_serial_number"></a> [camera\_serial\_number](#input\_camera\_serial\_number) | Enter the Serial Number of the camera e.g. `068745431065110085`.                                                                                                       | `string` | n/a     |   yes    |
| <a name="input_inventory_schedule"></a> [inventory\_schedule](#input\_inventory\_schedule)         | How often to update the inventory, valid options are number followed by 'm' for minutes, 'h' for hours, '1d' for daily, or 'immediate' for immediate inventory update. | `string` | `"12h"` |    no    |
| <a name="input_orchestrator_name"></a> [orchestrator\_name](#input\_orchestrator\_name)            | The name or GUID of the orchestrator that has been registered and approved in Keyfactor Command                                                                        | `string` | n/a     |   yes    |
| <a name="input_server_password"></a> [server\_password](#input\_server\_password)                  | The password to authenticate to the Bosch camera                                                                                                                       | `string` | n/a     |   yes    |
| <a name="input_server_use_ssl"></a> [server\_use\_ssl](#input\_server\_use\_ssl)                   | Whether to use SSL when connecting to the Bosch camera                                                                                                                 | `bool`   | `true`  |    no    |
| <a name="input_server_username"></a> [server\_username](#input\_server\_username)                  | The username to authenticate to the Bosch camera                                                                                                                       | `string` | n/a     |   yes    |

## Outputs

No outputs.
