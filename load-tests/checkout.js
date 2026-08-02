import http from 'k6/http';
import { check, sleep } from 'k6';

// Sprint 8 gate. Supply a non-production test token and a provisioned SKU via
// environment variables; no credentials are committed to source control.
export const options = {
  thresholds: {
    'http_req_duration{route:checkout}': ['p(95)<300'],
    'http_req_failed{route:checkout}': ['rate<0.01'],
  },
  scenarios: {
    checkout: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 10),
      timeUnit: '1s',
      duration: __ENV.DURATION || '1m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
};

const baseUrl = __ENV.BASE_URL || 'http://localhost:8080';
const token = __ENV.JWT;
const sku = __ENV.SKU;

function randomHex(length) {
  let value = '';
  for (let index = 0; index < length; index += 1) {
    value += Math.floor(Math.random() * 16).toString(16);
  }
  return value;
}

function uuidV7() {
  const timestamp = Date.now().toString(16).padStart(12, '0');
  const hex = `${timestamp}7${randomHex(3)}8${randomHex(3)}${randomHex(12)}`;
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export default function () {
  if (!token || !sku) {
    throw new Error('JWT and SKU environment variables are required. SKU must be provisioned in Inventory.');
  }

  const response = http.post(`${baseUrl}/api/v1/orders`, JSON.stringify({
    items: [{ sku, quantity: 1, unitPrice: 0 }],
  }), {
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      'Idempotency-Key': uuidV7(),
    },
    tags: { route: 'checkout' },
  });

  check(response, { 'order created': (r) => r.status === 201 });
  sleep(0.1);
}
