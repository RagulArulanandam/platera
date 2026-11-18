# Open questions

Nothing blocked the build. These are product/ops decisions worth revisiting:

1. **Token storage.** The SPA keeps access + refresh tokens in `localStorage` (simple, XSS-sensitive). Move the refresh token to an `HttpOnly; SameSite` cookie if you want stricter protection?
2. **Production hosting.** The SPA expects `/api`, `/seed` and `/uploads` on the same origin (dev proxy today). Which reverse proxy / hosting target should the deploy config assume?
3. **Image storage.** Local disk behind `IImageStorage`. Swap to Azure Blob / S3 for production?
4. **Email.** No email confirmation or password-reset flow yet (accounts are active immediately). Needed before public launch?
5. **Deactivated users.** Deactivation blocks login and revokes refresh tokens, but an already-issued 15-minute access token stays valid until it expires.
6. **Angular tests.** The UI is covered by `ng build` and manual checks; component/e2e tests are not written yet.
7. **Production secrets.** Provide `Jwt:Key`, `Seed:AdminPassword` and the real connection string through user-secrets / environment variables / a secret store.
