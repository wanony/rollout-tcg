# Security Policy

This is a portfolio/demonstration project and is **not intended for production use**. It contains intentionally simplified security configurations (dev credentials, developer signing keys) documented in the README.

If you spot a meaningful security issue, please open a GitHub Issue rather than a pull request.

## Known limitations

- API Gateway rate limiting does not validate `X-Forwarded-For` headers — client IP can be spoofed
- JWT sub is decoded manually in the rate limiter rather than via validated `HttpContext.User`
- Identity Service uses a developer signing credential (`AddDeveloperSigningCredential`) — keys are regenerated on restart
