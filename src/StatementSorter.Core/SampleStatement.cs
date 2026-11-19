namespace StatementSorter.Core;

public static class SampleStatement
{
    public const string FileName = "messy-statement.csv";
    public const string EarlierFileName = "earlier-statement.csv";

    public const string Csv =
        """
        Downloaded from Example Bank
        Account,****1234

        Transaction Date,Posted Date,Description,Category,Type,Amount
        01/03/2024,01/04/2024,WHOLE FOODS MARKET #102,,Sale,-86.42
        1/5/24,01/05/2024,STARBUCKS STORE 2211,,Sale,-6.15
        2024-01-07,2024-01-08,"AMAZON.COM*AB1CD2, SEATTLE",,Sale,"(42.18)"
        15-Jan-2024,01/16/2024,NETFLIX.COM,,Sale,-15.49
        01/18/2024 09:14:02,01/18/2024,SHELL OIL 5749,,Sale,-48.20
        "Jan 20, 2024",01/21/2024,UBER EATS *CHIPOTLE,,Sale,-23.40
        01/22/2024,01/22/2024,PAYROLL ACME INC,,Credit,"3,250.00"
        01/22/2024,01/22/2024,PAYROLL ACME INC,,Credit,"3,250.00"
        01/25/2024,01/25/2024,VENMO PAYMENT,,Payment,40.00
        not-a-date,01/26/2024,UNKNOWN MERCHANT,,Sale,-10.00
        01/28/2024,01/28/2024,,,Sale,
        02/01/2024,02/01/2024,RENT PAYMENT,,Sale,"-1,850.00"
        02/02/2024,02/02/2024,CVS/PHARMACY #4412,Pharmacy,Sale,-12.06
        Total Debits,,,,,-2000.00
        """;

    public const string EarlierCsv =
        """
        Downloaded from Example Bank
        Account,****1234

        Transaction Date,Posted Date,Description,Category,Type,Amount
        12/01/2023,12/01/2023,RENT PAYMENT,,Sale,"-1,850.00"
        12/02/2023,12/02/2023,WHOLE FOODS MARKET #102,,Sale,-90.00
        12/04/2023,12/04/2023,STARBUCKS STORE 2211,,Sale,-18.00
        12/08/2023,12/08/2023,UBER EATS *CHIPOTLE,,Sale,-40.00
        12/12/2023,12/12/2023,CHIPOTLE 4412,,Sale,-22.00
        12/14/2023,12/14/2023,AMAZON.COM,,Sale,-15.00
        12/15/2023,12/15/2023,NETFLIX.COM,,Sale,-15.49
        12/18/2023,12/18/2023,SHELL OIL 5749,,Sale,-40.00
        12/20/2023,12/20/2023,CVS/PHARMACY #4412,Pharmacy,Sale,-10.00
        12/31/2023,12/31/2023,PAYROLL ACME INC,,Credit,"3,250.00"
        """;
}
