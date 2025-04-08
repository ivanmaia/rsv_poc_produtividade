# Para rodar os servidores RabbitMq:

> docker-compose up -d


# Para acessar o RabbitMQ via browser:

Acesse: http://localhost:15672

Usuário/senha: guest/guest

Para conferir as filas e exchanges: na interface do RabbitMQ, vá em “Queues” ou “Exchanges”.

# Para acessar o SQL Server

## String de Conexão

"Server=localhost,1433;Database=PoC_DB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"


## Do SQL Server Management Studio (SSMS) ou Azure Data Studio:

Server name: localhost,1433

Authentication: “SQL Login”

Login: sa

Password: YourStrong@Passw0rd


## Como rodar o producer de eventos de indústria

Dentro da pasta raiz do repositorio:
> cd producer-industria
> dotnet build
> dotnet run


## Como rodar o consumer de eventos
Dentro da pasta raiz do repositorio:
> cd cd consumer
> dotnet build
> dotnet run


## Para testar e explorar os dados gerados

Conecte no banco de dados e execute queries nessas tabelas.

```SQL
use PoC_DB;

--Eventos
select * from PoC_DB.dbo.industria_evento with (nolock) order by identificadorExterno, data_hora_evento, etapa;

-- Tabela consolidada cicletime por lote
select * from PoC_DB.dbo.industria_ciclo with (nolock);

-- Tabela consolidada quantidade de itens produzidos por etapa por hora
select * from PoC_DB.dbo.industria_quantidade_etapa with (nolock);

-- Tabela consolidada 'cicle time' por etapa por hora
select * from PoC_DB.dbo.industria_tempo_etapa with (nolock);

-- Exemplo que consulta que seria realizada pelo backend: média de temmpo em cada etapa no mês
select 'CHECKOUT' as etapa, avg(tempo_medio_minutos) tempo_medio
  from PoC_DB.dbo.industria_tempo_etapa ite with (nolock)
  where ite.etapa = 'CHECKOUT' and ite.ano_evento = 2025 and ite.mes_evento = 4;
```