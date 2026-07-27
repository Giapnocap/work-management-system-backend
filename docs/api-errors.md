# API Error Contract

The backend returns a consistent JSON shape for errors.

```json
{
  "message": "Human-readable error message",
  "code": "business_error",
  "traceId": "0HN...",
  "details": "",
  "errors": null
}
```

## Error Codes

- `validation_error`: request data is invalid.
- `business_error`: the request is syntactically valid but violates a business rule.
- `not_found`: the requested resource does not exist or is no longer accessible.
- `forbidden`: the authenticated user does not have permission.
- `unauthorized`: the request is missing or has invalid identity information.
- `request_too_large`: the uploaded file or request body exceeds server limits.
- `bad_request`: malformed request data.
- `internal_server_error`: unexpected server-side failure.

## Exception Mapping

- `BusinessException` -> HTTP 400.
- `NotFoundException` -> HTTP 404.
- `ForbiddenException` -> HTTP 403.
- `UnauthorizedAccessException` -> HTTP 403 fallback for legacy code.
- `BadHttpRequestException` with request-size errors -> HTTP 413.
- unexpected exceptions -> HTTP 500.

## Notes

- Every response includes `X-Correlation-ID`. A safe client-supplied value is reused; otherwise the server trace identifier is returned.
- The JSON `traceId`, response correlation header, and structured request log share the same identifier.
- Services should throw explicit application exceptions instead of generic `Exception`.
- Controllers should return an error body with at least `message` and `code` when they handle an error directly.
- Authenticated controllers should resolve the current user id from token claims and return `unauthorized` when the id is missing or invalid.
- DTO/model validation failures return the same JSON shape with `code = validation_error` and field-level `errors`.
- Internal exception details are included only in development mode.
- Client-disconnected requests are treated as cancellation and are not converted into HTTP 500 responses.
- Swagger documents common `400`, `401`, `403`, and `500` responses where applicable, but this document remains the source of truth for the response body shape.
