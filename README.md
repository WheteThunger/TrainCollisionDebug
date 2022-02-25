## What this plugin does

Ever seen these warnings?

```
GetTotalPushingMass: Recursive loop detected. Bailing out.
GetTotalPushingForces: Recursive loop detected. Bailing out.
StackOverflowException: The requested operation caused a stack overflow.
Server Exception: Mountable Cycle
```

These warnings may print in your server console when something goes very wrong with workcart collisions. The first two mean that Rust detected an issue and tried to prevent your server from freezing/crashing, but it doesn't always work. If it fails to mitigate the issue, the later two errors will show up as well, which can freeze and even crash the server. This plugin aims to detect such scenarios, print diagnostic information to help you to determine the cause, and automatically slows or destroys the workcarts in question to prevent them from crashing the server.
