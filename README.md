# CrystalC2

Command and control framework for funsies.

## Authentication

The team server uses `appsettings.json` to control authentication.  Set the JWT key to something random and set the password that clients will use to connect.

```json
  "Jwt": {
    "Key": "CHANGE-THIS-TO-A-SECURE-RANDOM-KEY-AT-LEAST-32-CHARS"
  },
  "Auth": {
    "Password": "changeme"
  }
```