# R4_remote_BrokerRPC (Laptop Client)

## Desktop PC (server) start commands
```powershell
cd "C:\Users\Kubirill\3D Objects\Adaptation Service"

# Open firewall (run in elevated PowerShell)
.\Experiments\open_firewall_rabbitmq.ps1

# Optional: set broker credentials (defaults to adaptation/adaptation)
$env:BROKER_USER="adaptation"
$env:BROKER_PASS="adaptation"

# Start RabbitMQ + worker
.\Experiments\start_rabbitmq_server.ps1
.\Experiments\start_broker_worker_server.ps1
```

## Find desktop PC IP
```powershell
ipconfig
```
Look for the IPv4 address on the private network (e.g., 192.168.x.x).

## Laptop commands after git pull
```powershell
cd "C:\Users\Kubirill\3D Objects\Adaptation Service"

# Set broker connection settings
$env:BROKER_HOST="<desktop_ip>"
$env:BROKER_PORT="5672"
$env:BROKER_USER="adaptation"
$env:BROKER_PASS="adaptation"

# Quick connectivity check
.\Experiments\check_R4_broker_client.ps1

# Run R4 experiment
.\Experiments\run_R4_broker_client.ps1 -Trials 5 -Sessions 30
```

## Notes
- Ensure the desktop PC network profile is **Private** and firewall rules are applied.
- The RabbitMQ management UI is available at `http://<desktop_ip>:15672`.
