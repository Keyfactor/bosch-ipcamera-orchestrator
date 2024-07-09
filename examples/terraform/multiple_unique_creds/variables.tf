# Copyright 2024 Keyfactor
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http:#www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

variable "orchestrator_name" {
  type        = string
  description = "The name or GUID of the orchestrator that has been registered and approved in Keyfactor Command"
}

variable "camera_map" {
  type        = map(object({
    ip       = string
    username = string
    password = string
  }))
  description = "A map containing the serial number to IP address, username and password of the cameras to be enrolled"
}

variable "server_username" {
  type        = string
  sensitive   = true
  description = "The username to authenticate to the Bosch camera"
}
variable "server_password" {
  type        = string
  sensitive   = true
  description = "The password to authenticate to the Bosch camera"
}
variable "inventory_schedule" {
  type        = string
  description = "How often to update the inventory, valid options are number followed by 'm' for minutes, 'h' for hours, '1d' for daily, or 'immediate' for immediate inventory update."
  default     = "12h"
}

variable "server_use_ssl" {
  default     = true
  type        = bool
  description = "Whether to use SSL when connecting to the Bosch camera"
}