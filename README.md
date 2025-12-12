# Микросервисная система заказов и платежей

## Описание

Система состоит из следующих микросервисов:
- **Payments Service** - управление счетами и платежами
- **Orders Service** - управление заказами
- **API Gateway** - единая точка входа для всех запросов
- **Frontend** - веб-интерфейс

## Архитектура

Система использует:
- **Transactional Outbox** в Orders Service для публикации событий оплаты
- **Transactional Inbox и Outbox** в Payments Service для обработки платежей и публикации результатов
- **RabbitMQ** для асинхронной коммуникации между сервисами
- **SQL Server** для хранения данных
- **Идемпотентность** для обеспечения exactly-once семантики при списании средств

## Запуск через Docker Compose

```bash
docker compose up -d
```

Сервисы будут доступны по следующим адресам:
- Frontend: http://localhost
- API Gateway: http://localhost:8080
- Payments Service: http://localhost:8081
- Orders Service: http://localhost:8082
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- Swagger:
  - Payments: http://localhost:8081/swagger
  - Orders: http://localhost:8082/swagger

## API Endpoints

### Payments Service

- `POST /api/payments/accounts` - Создать счет
- `GET /api/payments/accounts/balance` - Получить баланс
- `POST /api/payments/accounts/topup` - Пополнить счет

Все запросы требуют заголовок `X-User-Id`.

### Orders Service

- `POST /api/orders` - Создать заказ
- `GET /api/orders` - Получить список заказов
- `GET /api/orders/{orderId}` - Получить заказ по ID

Все запросы требуют заголовок `X-User-Id`.

## Запуск тестов

### Локально

```bash
dotnet test
```

## Использование

1. Откройте http://localhost в браузере
2. Установите User ID (по умолчанию: user-123)
3. Создайте счет
4. Пополните счет
5. Создайте заказ - он автоматически будет оплачен
6. Просмотрите список заказов и их статусы

## Технологии

- .NET 8.0
- ASP.NET Core Web API
- Entity Framework Core
- RabbitMQ
- SQL Server
- Docker & Docker Compose
- YARP (Yet Another Reverse Proxy)
- xUnit для тестирования
