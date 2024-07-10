import argparse
import csv
import os
import sys

DEFAULT_OUTPUT_TFVARS_FILE = 'BoschIPCameraStores.tfvars'

def validate_file_exists(file_path):
    if not os.path.exists(file_path):
        print(f"Error: The file '{file_path}' does not exist.")
        sys.exit(1)

def get_args(interactive):
    parser = argparse.ArgumentParser(description="""
    Convert CSV to TFVARS. This script parses a given CSV file containing camera information and generates a Terraform variables file (.tfvars) with the data structured for Terraform usage.

    Usage:
        csv2tfvars.py -csv <input_csv_file> -orch <orchestrator_name> [output_tfvars_file] [-i]
        csv2tfvars.py --help

    The -i flag enables interactive mode, prompting for any missing required inputs.""",
    formatter_class=argparse.RawTextHelpFormatter)

    parser.add_argument('-csv', '--csv_file', type=str, required=False, help='Path to the input CSV file. Required unless in interactive mode.')
    parser.add_argument('-u', '--server_username', type=str, required=False, help='Username for IP cameras. Required unless in interactive mode.')
    parser.add_argument('-p', '--server_password', type=str, required=False, help='Password for IP cameras. Required unless in interactive mode.')
    parser.add_argument('-orch', '--orchestrator_name', type=str, required=False, help='Orchestrator client name. Required unless in interactive mode.')
    parser.add_argument('output_tfvars_file', nargs='?', default=DEFAULT_OUTPUT_TFVARS_FILE, help='Output TFVARS file path. Optional, defaults to BoschIPCameraStores.tfvars.')
    parser.add_argument('-i', '--interactive', action='store_true', help='Run in interactive mode. Prompts for missing inputs.')

    args = parser.parse_args()

    if interactive:
        if not args.csv_file:
            args.csv_file = input("Enter the input CSV file path: ")
        if not args.server_username:
            args.server_username = input("Enter the server username: ")
        if not args.server_password:
            args.server_password = input("Enter the server password: ")
        if not args.orchestrator_name:
            args.orchestrator_name = input("Enter the orchestrator_name: ")
        if args.output_tfvars_file == DEFAULT_OUTPUT_TFVARS_FILE:  # Default value
            args.output_tfvars_file = input("Enter the output TFVARS file path (default is 'BoschIPCameraStores.tfvars'): ") or DEFAULT_OUTPUT_TFVARS_FILE
    else:
        if not args.csv_file or not args.orchestrator_name:
            parser.print_help()
            sys.exit(1)

    validate_file_exists(args.csv_file)
    return args

def main():
    args = get_args('-i' in sys.argv)

    camera_map = {}
    with open(args.csv_file, mode='r', encoding='utf-8') as csvfile:
        reader = csv.DictReader(csvfile)
        for row in reader:
            camera_map[row['serial_number']] = {
                'ip': row['ip'],
            }

    with open(args.output_tfvars_file, mode='w', encoding='utf-8') as tfvarsfile:
        tfvarsfile.write(f'orchestrator_name="{args.orchestrator_name}"\n')
        tfvarsfile.write(f'server_username="{args.server_username}"\n')
        tfvarsfile.write(f'server_password="{args.server_password}"\n')
        tfvarsfile.write('camera_map = {\n')
        for serial, details in camera_map.items():
            tfvarsfile.write(f'  "{serial}" = "{details["ip"]}"\n')
        tfvarsfile.write('}\n')
    print(f"TFVARS file generated: {args.output_tfvars_file}")

if __name__ == "__main__":
    main()