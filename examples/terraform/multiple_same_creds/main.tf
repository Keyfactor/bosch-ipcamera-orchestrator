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

resource "keyfactor_certificate_store" "bosch_camera_store" {
  for_each = var.camera_map
  client_machine     = each.value//this is camera IP
  store_path         = each.key //this is camera serial number
  agent_identifier   = data.keyfactor_agent.universal_orchestrator.agent_identifier
  store_type = "BIPCamera" # Must exist in KeyFactor Command
  server_username    = var.server_username
  server_password    = var.server_password
  server_use_ssl     = var.server_use_ssl
  inventory_schedule = var.inventory_schedule
}
