# ADR 0012: Consumer-Driven Contract (CDC) Testing with PactNet

## Status
Accepted

## Context
As the number of microservices grows, ensuring that changes in a provider API (e.g., `Ordering.Api`) do not break consumer contracts (e.g., `Payments.Api` or `Gateway`) becomes challenging. End-to-end (E2E) tests are slow, brittle, and require complex environment setups. Traditional mocked unit tests do not guarantee that the actual API matches the mocked responses.

## Decision
We adopted **Consumer-Driven Contract (CDC) Testing** using `PactNet` integrated with `xUnit`.
- Consumers write tests defining their expectations (the "Contract") from the provider API.
- These expectations generate a Pact JSON file.
- The Provider runs a verification test against the generated Pact file to ensure its actual endpoints fulfill the consumer's expectations.

## Consequences
### Positive
- **Fast Feedback:** Contract tests run as fast as unit tests since they don't require standing up the entire system.
- **Confidence:** Deployments are safer because providers are mathematically proven to fulfill consumer contracts before CI/CD allows a release.
- **Decoupling:** Teams can work independently without needing shared staging environments.

### Negative
- **Learning Curve:** Developers must learn the Pact framework and the CDC mental model (Consumer dictates the contract).
- **Maintenance:** Contract files must be shared between repositories (or via a Pact Broker) during the CI/CD pipeline.
