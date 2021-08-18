# ReadSierraChartSCIDSharp
C# version of program to read Sierra Chart(tm) SCID stock/futures binary data files

This program reads Sierra Chart .scid files from C:/SierraChart/Data directory and writes filtered CSV files to the local SierraChartData directory.
On my machine this is at C:/Users/larry/SierraChartData. I have a private GitHub repo named SierraChartData which also has this data

Each input file name is of the form {futures prefix}{futures month code}{2 digit year}{maybe other stuff}.scid
It is a binary file whose format is specified in Sierra Chart documentation. Basically each file consists of a header and a number of data records. I have copied the
header files in this repo from the Sierra Chart directory.

Unlike each .scid file, which contains tick data for the entire contract, 
the written CSV files only contain at most 1 tick per second starting from the 2200 UTC on the 9th of the first active month to 2200 UTC on the 9th of the expiration month,
a total of 3 months. This is equivalent to 6pm ET on the 9th of the expiry month-3 through 6pm ET on the 9th of the expiry month. For each day, there is data from 6pm ET
through 4:30pm ET of the following day...the current hours (as of 8/5/2021) of the CME futures contracts. For each week, there is data from 6pm ET Sunday through 4:30pm ET Friday.

Each tick is written with an ISO date/time format in the form:

yyyy-mm-ddThh:mm:ss,price

price is a floating point number with 2 digitd to the right of the decimal point.

# Programming comments:
This is written using C# 10 and Visual Studio 2022 Preview