# C# NEDF reader

## NEDF reader for Analyzer2

This is a reader component to allow
[Brainvision Analyzer](https://www.brainproducts.com/productdetails.php?id=17)
to read
[NEDF](https://www.neuroelectrics.com/wiki/index.php/Files_%26_Formats#The_.nedf_.28binary.29_data_format)
files (commonly produced by Enobio / StarStim EEG headsets).

### Installation

Simply copy the [`dll` files](https://github.com/tstenner/nedfreader/releases/latest)
to your Analyzer directory or add the downloaded folder to the analyzer config:

![Analyzer admin screenshot](analyzeradmin.png)

## NedfExport

```
NedfExport:
  Export header / marker data for NEDF files

Usage:
  NedfExport [options] <files>...

Arguments:
  <files>    NEDF files to process

Options:
  -o, --statsfile <statsfile>    File to save stats to instead of stdout
  --errlog <errlog>              File to log errors to instead of stderr
  --maxsamples <maxsamples>      Quit after this many samples, useful for sanity checks [default: 2147483647]
  --markercsv                    Write a CSV file with markers for each supplied nedf file
  --version                      Show version information
  -?, -h, --help                 Show help and usage information
 ```

 For Windows users: drag one or more nedf files onto `NedfExport.exe`.
