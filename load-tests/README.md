# Checkout load gate

Run with a short-lived Keycloak test JWT and a SKU provisioned in both Catalog and Inventory:

```powershell
$env:BASE_URL='http://localhost:8080'
$env:JWT='<test-token>'
$env:SKU='TEST-SKU-001'
k6 run .\load-tests\checkout.js
```

The run fails if checkout p95 exceeds 300 ms or the error rate reaches 1%.
