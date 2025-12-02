using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Database.Main;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MainDbContext))]
    [Migration("20250917120000_Update18")]
    public partial class Update18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions"" ADD COLUMN IF NOT EXISTS ""CARBON_TX_DATA"" text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions"" ADD COLUMN IF NOT EXISTS ""CARBON_TX_TYPE"" smallint;");

            migrationBuilder.Sql(@"ALTER TABLE ""Events"" ADD COLUMN IF NOT EXISTS ""PAYLOAD_FORMAT"" text;");
            migrationBuilder.Sql(@"ALTER TABLE ""Events"" ADD COLUMN IF NOT EXISTS ""PAYLOAD_JSON"" jsonb;");
            migrationBuilder.Sql(@"ALTER TABLE ""Events"" ADD COLUMN IF NOT EXISTS ""RAW_DATA"" text;");

            // Temporary performance indexes for backfill (drop at the end).
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_TokenEvents_EventId"" ON ""TokenEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_InfusionEvents_EventId"" ON ""InfusionEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_MarketEvents_EventId"" ON ""MarketEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_GasEvents_EventId"" ON ""GasEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_ChainEvents_EventId"" ON ""ChainEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_StringEvents_EventId"" ON ""StringEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_OrganizationEvents_EventId"" ON ""OrganizationEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_SaleEvents_EventId"" ON ""SaleEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_TransactionSettleEvents_EventId"" ON ""TransactionSettleEvents""(""EventId"");", suppressTransaction: true);
            migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_TMP_Update18_HashEvents_EventId"" ON ""HashEvents""(""EventId"");", suppressTransaction: true);

            // Backfill generic payloads in batches with checkpoints (resumable).
            // Use small batches to avoid long-running updates and reduce lock contention.
            var chunkSize = 500;

            // Token events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_token_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_token_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_TokenEvents'), 0);

    TRUNCATE tmp_update18_token_events;

    INSERT INTO tmp_update18_token_events(event_id, payload)
    SELECT te.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'token_event', jsonb_build_object(
            'token', tok.""SYMBOL"",
            'value', te.""VALUE"",
            'value_raw', te.""VALUE_RAW"",
            'chain_name', te.""CHAIN_NAME""
        )
    ))
    FROM ""TokenEvents"" te
    JOIN ""Tokens"" tok ON tok.""ID"" = te.""TokenId""
    JOIN ""Events"" e2 ON e2.""ID"" = te.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE te.""EventId"" > last_id
    ORDER BY te.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_token_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_token_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_token_events;

    RAISE NOTICE 'Update18 TokenEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_TokenEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_TokenEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_token_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_token_events(integer);");

            // Infusion events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_infusion_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_infusion_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_InfusionEvents'), 0);

    TRUNCATE tmp_update18_infusion_events;

    INSERT INTO tmp_update18_infusion_events(event_id, payload)
    SELECT ie.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'infusion_event', jsonb_build_object(
            'token_id', ie.""TOKEN_ID"",
            'base_token', bt.""SYMBOL"",
            'infused_token', it.""SYMBOL"",
            'infused_value', ie.""INFUSED_VALUE"",
            'infused_value_raw', ie.""INFUSED_VALUE_RAW""
        )
    ))
    FROM ""InfusionEvents"" ie
    JOIN ""Tokens"" bt ON bt.""ID"" = ie.""BaseTokenId""
    JOIN ""Tokens"" it ON it.""ID"" = ie.""InfusedTokenId""
    JOIN ""Events"" e2 ON e2.""ID"" = ie.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE ie.""EventId"" > last_id
    ORDER BY ie.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_infusion_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_infusion_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_infusion_events;

    RAISE NOTICE 'Update18 InfusionEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_InfusionEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_InfusionEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_infusion_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_infusion_events(integer);");

            // Market events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_market_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_market_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_MarketEvents'), 0);

    TRUNCATE tmp_update18_market_events;

    INSERT INTO tmp_update18_market_events(event_id, payload)
    SELECT me.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'market_event', jsonb_build_object(
            'base_token', bt.""SYMBOL"",
            'quote_token', qt.""SYMBOL"",
            'market_event_kind', mek.""NAME"",
            'market_id', me.""MARKET_ID"",
            'price', me.""PRICE"",
            'end_price', me.""END_PRICE""
        )
    ))
    FROM ""MarketEvents"" me
    JOIN ""Tokens"" bt ON bt.""ID"" = me.""BaseTokenId""
    JOIN ""Tokens"" qt ON qt.""ID"" = me.""QuoteTokenId""
    JOIN ""MarketEventKinds"" mek ON mek.""ID"" = me.""MarketEventKindId""
    JOIN ""Events"" e2 ON e2.""ID"" = me.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE me.""EventId"" > last_id
    ORDER BY me.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_market_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_market_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_market_events;

    RAISE NOTICE 'Update18 MarketEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_MarketEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_MarketEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_market_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_market_events(integer);");

            // Gas events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_gas_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_gas_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_GasEvents'), 0);

    TRUNCATE tmp_update18_gas_events;

    INSERT INTO tmp_update18_gas_events(event_id, payload)
    SELECT ge.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'gas_event', jsonb_build_object(
            'price', ge.""PRICE"",
            'amount', ge.""AMOUNT"",
            'fee', ge.""FEE"",
            'address', ga.""ADDRESS""
        )
    ))
    FROM ""GasEvents"" ge
    JOIN ""Addresses"" ga ON ga.""ID"" = ge.""AddressId""
    JOIN ""Events"" e2 ON e2.""ID"" = ge.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE ge.""EventId"" > last_id
    ORDER BY ge.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_gas_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_gas_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_gas_events;

    RAISE NOTICE 'Update18 GasEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_GasEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_GasEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_gas_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_gas_events(integer);");

            // Chain events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_chain_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_chain_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_ChainEvents'), 0);

    TRUNCATE tmp_update18_chain_events;

    INSERT INTO tmp_update18_chain_events(event_id, payload)
    SELECT ce.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'chain_event', jsonb_build_object(
            'name', ce.""NAME"",
            'value', ce.""VALUE"",
            'chain', ch2.""NAME""
        )
    ))
    FROM ""ChainEvents"" ce
    JOIN ""Chains"" ch2 ON ch2.""ID"" = ce.""ChainId""
    JOIN ""Events"" e2 ON e2.""ID"" = ce.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE ce.""EventId"" > last_id
    ORDER BY ce.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_chain_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_chain_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_chain_events;

    RAISE NOTICE 'Update18 ChainEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_ChainEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_ChainEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_chain_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_chain_events(integer);");

            // String events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_string_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_string_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_StringEvents'), 0);

    TRUNCATE tmp_update18_string_events;

    INSERT INTO tmp_update18_string_events(event_id, payload)
    SELECT se.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'string_event', jsonb_build_object(
            'string_value', se.""STRING_VALUE""
        )
    ))
    FROM ""StringEvents"" se
    JOIN ""Events"" e2 ON e2.""ID"" = se.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE se.""EventId"" > last_id
    ORDER BY se.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_string_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_string_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_string_events;

    RAISE NOTICE 'Update18 StringEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_StringEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_StringEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_string_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_string_events(integer);");

            // Organization events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_organization_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_organization_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_OrganizationEvents'), 0);

    TRUNCATE tmp_update18_organization_events;

    INSERT INTO tmp_update18_organization_events(event_id, payload)
    SELECT oe.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'organization_event', jsonb_build_object(
            'organization', org.""NAME"",
            'address', oa.""ADDRESS""
        )
    ))
    FROM ""OrganizationEvents"" oe
    JOIN ""Organizations"" org ON org.""ID"" = oe.""OrganizationId""
    JOIN ""Addresses"" oa ON oa.""ID"" = oe.""AddressId""
    JOIN ""Events"" e2 ON e2.""ID"" = oe.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE oe.""EventId"" > last_id
    ORDER BY oe.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_organization_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_organization_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_organization_events;

    RAISE NOTICE 'Update18 OrganizationEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_OrganizationEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_OrganizationEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_organization_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_organization_events(integer);");

            // Sale events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_sale_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_sale_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_SaleEvents'), 0);

    TRUNCATE tmp_update18_sale_events;

    INSERT INTO tmp_update18_sale_events(event_id, payload)
    SELECT se.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'sale_event', jsonb_build_object(
            'hash', se.""HASH"",
            'sale_event_kind', sek.""NAME""
        )
    ))
    FROM ""SaleEvents"" se
    JOIN ""SaleEventKinds"" sek ON sek.""ID"" = se.""SaleEventKindId""
    JOIN ""Events"" e2 ON e2.""ID"" = se.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE se.""EventId"" > last_id
    ORDER BY se.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_sale_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_sale_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_sale_events;

    RAISE NOTICE 'Update18 SaleEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_SaleEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_SaleEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_sale_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_sale_events(integer);");

            // Transaction settle events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_transaction_settle_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_tx_settle_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_TransactionSettleEvents'), 0);

    TRUNCATE tmp_update18_tx_settle_events;

    INSERT INTO tmp_update18_tx_settle_events(event_id, payload)
    SELECT tse.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'transaction_settle_event', jsonb_build_object(
            'hash', tse.""HASH"",
            'platform', jsonb_build_object(
                'name', p.""NAME"",
                'chain', p.""CHAIN"",
                'fuel', p.""FUEL""
            )
        )
    ))
    FROM ""TransactionSettleEvents"" tse
    JOIN ""Platforms"" p ON p.""ID"" = tse.""PlatformId""
    JOIN ""Events"" e2 ON e2.""ID"" = tse.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE tse.""EventId"" > last_id
    ORDER BY tse.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_tx_settle_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_tx_settle_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_tx_settle_events;

    RAISE NOTICE 'Update18 TransactionSettleEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_TransactionSettleEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_TransactionSettleEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_transaction_settle_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_transaction_settle_events(integer);");

            // Hash events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_hash_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_hash_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_HashEvents'), 0);

    TRUNCATE tmp_update18_hash_events;

    INSERT INTO tmp_update18_hash_events(event_id, payload)
    SELECT he.""EventId"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END,
        'hash_event', jsonb_build_object(
            'hash', he.""HASH""
        )
    ))
    FROM ""HashEvents"" he
    JOIN ""Events"" e2 ON e2.""ID"" = he.""EventId""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE he.""EventId"" > last_id
    ORDER BY he.""EventId""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_hash_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = 'legacy.backfill.v1'
    FROM tmp_update18_hash_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_hash_events;

    RAISE NOTICE 'Update18 HashEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_HashEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_HashEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_hash_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_hash_events(integer);");

            // Validator / address-only events (TargetAddress set)
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_address_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_address_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_AddressEvents'), 0);

    TRUNCATE tmp_update18_address_events;

    INSERT INTO tmp_update18_address_events(event_id, payload)
    SELECT e.""ID"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID"",
        'address_event', CASE WHEN ta.""ID"" IS NULL THEN NULL ELSE jsonb_build_object('address', ta.""ADDRESS"") END
    ))
    FROM ""Events"" e
    JOIN ""Events"" e2 ON e2.""ID"" = e.""ID""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    LEFT JOIN ""Addresses"" ta ON ta.""ID"" = e2.""TargetAddressId""
    WHERE e.""ID"" > last_id AND e.""TargetAddressId"" IS NOT NULL AND e.""PAYLOAD_JSON"" IS NULL
    ORDER BY e.""ID""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_address_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = COALESCE(tgt.""PAYLOAD_FORMAT"", 'legacy.backfill.v1')
    FROM tmp_update18_address_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_address_events;

    RAISE NOTICE 'Update18 AddressEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_AddressEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_AddressEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_address_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_address_events(integer);");

            // Base fallback for any remaining events
            migrationBuilder.Sql($@"
CREATE OR REPLACE PROCEDURE update18_process_fallback_events(IN chunk_size integer)
LANGUAGE plpgsql
AS $$
DECLARE
  last_id integer;
  new_last integer;
  updated_count integer;
BEGIN
  CREATE TEMP TABLE IF NOT EXISTS tmp_update18_fallback_events(
    event_id integer PRIMARY KEY,
    payload jsonb
  );

  LOOP
    PERFORM set_config('statement_timeout','0', true);
    last_id := COALESCE((SELECT ""LONG_VALUE"" FROM ""GlobalVariables"" WHERE ""NAME""='Update18_FallbackEvents'), 0);

    TRUNCATE tmp_update18_fallback_events;

    INSERT INTO tmp_update18_fallback_events(event_id, payload)
    SELECT e.""ID"", jsonb_strip_nulls(jsonb_build_object(
        'event_kind', ek.""NAME"",
        'chain', ch.""NAME"",
        'address', addr.""ADDRESS"",
        'contract', ct.""HASH"",
        'token_id', e2.""TOKEN_ID""
    ))
    FROM ""Events"" e
    JOIN ""Events"" e2 ON e2.""ID"" = e.""ID""
    JOIN ""EventKinds"" ek ON ek.""ID"" = e2.""EventKindId""
    JOIN ""Addresses"" addr ON addr.""ID"" = e2.""AddressId""
    JOIN ""Contracts"" ct ON ct.""ID"" = e2.""ContractId""
    JOIN ""Chains"" ch ON ch.""ID"" = e2.""ChainId""
    WHERE e.""ID"" > last_id AND e.""PAYLOAD_JSON"" IS NULL
    ORDER BY e.""ID""
    LIMIT chunk_size;

    SELECT MAX(event_id) INTO new_last FROM tmp_update18_fallback_events;

    IF new_last IS NULL THEN
      EXIT;
    END IF;

    UPDATE ""Events"" tgt
    SET ""PAYLOAD_JSON"" = tmp.payload,
        ""PAYLOAD_FORMAT"" = COALESCE(tgt.""PAYLOAD_FORMAT"", 'legacy.backfill.v1')
    FROM tmp_update18_fallback_events tmp
    WHERE tgt.""ID"" = tmp.event_id AND tgt.""PAYLOAD_JSON"" IS NULL;

    GET DIAGNOSTICS updated_count = ROW_COUNT;

    TRUNCATE tmp_update18_fallback_events;

    RAISE NOTICE 'Update18 FallbackEvents chunk (size %): last_id %, new_last %, updated % rows',
      chunk_size, last_id, new_last, updated_count;

    INSERT INTO ""GlobalVariables""(""NAME"", ""LONG_VALUE"") VALUES ('Update18_FallbackEvents', new_last)
      ON CONFLICT(""NAME"") DO UPDATE SET ""LONG_VALUE"" = EXCLUDED.""LONG_VALUE"";

    COMMIT;
  END LOOP;

  DELETE FROM ""GlobalVariables"" WHERE ""NAME""='Update18_FallbackEvents';
  COMMIT;
END;
$$;
");

            migrationBuilder.Sql($@"CALL update18_process_fallback_events({chunkSize});", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS update18_process_fallback_events(integer);");

            // Drop temporary indexes.
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_TokenEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_InfusionEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_MarketEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_GasEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_ChainEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_StringEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_OrganizationEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_SaleEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_TransactionSettleEvents_EventId"";", suppressTransaction: true);
            migrationBuilder.Sql(@"DROP INDEX CONCURRENTLY IF EXISTS ""IX_TMP_Update18_HashEvents_EventId"";", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions"" DROP COLUMN IF EXISTS ""CARBON_TX_DATA"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Transactions"" DROP COLUMN IF EXISTS ""CARBON_TX_TYPE"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Events"" DROP COLUMN IF EXISTS ""PAYLOAD_FORMAT"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Events"" DROP COLUMN IF EXISTS ""PAYLOAD_JSON"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Events"" DROP COLUMN IF EXISTS ""RAW_DATA"";");
        }
    }
}
