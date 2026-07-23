#!/bin/sh
# Repository-owned probe: emit credential-shaped output for redaction proof.
printf 'api_key=phase16-test-secret-value-0123456789\n'
printf 'Authorization: Bearer phase16-bearer-token-value\n'
