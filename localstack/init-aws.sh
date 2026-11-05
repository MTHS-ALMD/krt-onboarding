#!/bin/bash
# Cria o tópico SNS de eventos de conta e uma fila SQS por área consumidora (fan-out).
set -e

TOPIC_ARN=$(awslocal sns create-topic --name krt-account-events --query TopicArn --output text)

subscribe() {
  local queue=$1 filter=$2
  local url arn sub
  url=$(awslocal sqs create-queue --queue-name "$queue" --query QueueUrl --output text)
  arn=$(awslocal sqs get-queue-attributes --queue-url "$url" --attribute-names QueueArn --query Attributes.QueueArn --output text)
  sub=$(awslocal sns subscribe --topic-arn "$TOPIC_ARN" --protocol sqs --notification-endpoint "$arn" \
        --attributes RawMessageDelivery=true --query SubscriptionArn --output text)
  if [ -n "$filter" ]; then
    awslocal sns set-subscription-attributes --subscription-arn "$sub" \
      --attribute-name FilterPolicy --attribute-value "$filter"
  fi
}

# Prevenção a fraude recebe todos os eventos de conta.
subscribe fraud-prevention-account-events ""
# Cartões só precisa saber de contas criadas (filtro pelo atributo eventType).
subscribe cards-account-events '{"eventType":["AccountCreated"]}'

echo "SNS/SQS prontos: $TOPIC_ARN"
