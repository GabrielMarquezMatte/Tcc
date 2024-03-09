WITH "HistData" AS (
    SELECT "Date",
        LN(
            CASE
                WHEN "Adjusted" < 0 THEN "Close"
                ELSE "Adjusted"
            END
        ) - LAG(
            LN(
                CASE
                    WHEN "Adjusted" < 0 THEN "Close"
                    ELSE "Adjusted"
                END
            ),
            1,
            LN(
                CASE
                    WHEN "Adjusted" < 0 THEN "Close"
                    ELSE "Adjusted"
                END
            )
        ) OVER(
            PARTITION BY "TickerId"
            ORDER BY "Date"
        ) AS "LogReturn"
    FROM "HistoricalDataYahoo"
    WHERE "Date" >= '2024-01-01'
    AND "Volume" <> 0
)
SELECT "Date", EXP("LogReturn") - 1 AS "Return"
FROM "HistData"