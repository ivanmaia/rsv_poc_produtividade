-- Crie o DB (caso queira automatizar):
IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = 'PoC_DB')
BEGIN
    CREATE DATABASE PoC_DB;
END
GO

USE PoC_DB;
GO

-------------------------------------------------------------------------------
-- Tabela de eventos de indústria (atividade_inicio_termino):
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.industria_evento') IS NOT NULL
    DROP TABLE dbo.industria_evento;
GO

CREATE TABLE dbo.industria_evento (
    id                  VARCHAR(36)  NOT NULL, -- GUID
    origem              INT          NOT NULL, -- 0 - indústria
    processo            VARCHAR(100) NOT NULL,
    canal               VARCHAR(50)  NOT NULL,
    etapa               VARCHAR(50)  NOT NULL,
    identificadorExterno VARCHAR(100) NOT NULL,
    status              INT          NOT NULL, -- 0 - início, 1 - fim, 2 - reinício
    operador_id         VARCHAR(100) NOT NULL,
    operador_nome       VARCHAR(150) NOT NULL,
    quantidade_itens    INT          NOT NULL,
    tipo_etapa_no_ciclo INT          NOT NULL, -- 0 - inicial, 1 - intermediário, 2 - final
    data_hora_evento    DATETIME     NOT NULL,

    -- Desnormalizados para agrupamento rápido:
    data_evento         DATE         NOT NULL,
    hora_evento         INT          NOT NULL,
    mes_evento          INT          NOT NULL,
    ano_evento          INT          NOT NULL,

    CONSTRAINT PK_industria_evento PRIMARY KEY (id)
);

-------------------------------------------------------------------------------
-- Tabelas extras de agregação, se desejar (opcional para esta PoC):
-- Exemplo: industria_ciclo, industria_quantidade_etapa
-------------------------------------------------------------------------------
IF OBJECT_ID('dbo.industria_ciclo') IS NOT NULL
    DROP TABLE dbo.industria_ciclo;
GO

CREATE TABLE dbo.industria_ciclo (
    identificadorExterno   VARCHAR(100) NOT NULL,
    inicio_ciclo           DATETIME     NOT NULL,
    fim_ciclo              DATETIME     NOT NULL,
    tempo_ciclo_minutos    INT          NOT NULL,
    ano_evento             INT          NOT NULL,
    mes_evento             INT          NOT NULL,
    dia_evento             DATE         NOT NULL,
    hora_evento            INT          NOT NULL,

    CONSTRAINT PK_industria_ciclo
        PRIMARY KEY (identificadorExterno, fim_ciclo)
);

IF OBJECT_ID('dbo.industria_quantidade_etapa') IS NOT NULL
    DROP TABLE dbo.industria_quantidade_etapa;
GO

CREATE TABLE dbo.industria_quantidade_etapa (
    etapa               VARCHAR(50) NOT NULL,
    ano_evento          INT         NOT NULL,
    mes_evento          INT         NOT NULL,
    dia_evento          DATE        NOT NULL,
    hora_evento         INT         NOT NULL,
    total_itens         INT         NOT NULL,

    CONSTRAINT PK_industria_quantidade_etapa
        PRIMARY KEY (etapa, ano_evento, mes_evento, dia_evento, hora_evento)
);

CREATE TABLE dbo.industria_tempo_etapa (
    etapa               VARCHAR(50) NOT NULL,
    ano_evento          INT         NOT NULL,
    mes_evento          INT         NOT NULL,
    dia_evento          DATE        NOT NULL,
    hora_evento         INT         NOT NULL,
    
    tempo_medio_minutos INT         NOT NULL,

    PRIMARY KEY (etapa, ano_evento, mes_evento, dia_evento, hora_evento)
);

