# PDF Downloader - User Guide

Downloads pdf files from excel sheet.

## Getting Started

- Under releases section on GitHub (on the right side) find the most recent release and click on it.
- Download the zip file at the top (not source code).
- Extract file and in the folder publish there will be a PdfDownloaderWPF.exe file.
- Run the .exe.
- Windows may show a warning ("Windows protected your PC") because the program isn't digitally signed. If this happens, click More info, then Run anyway.

## How to Use It

### Step 1: Prepare Your Excel File

Your input Excel file must have three columns, starting from row 2 (row 1 should be headers):

| Column | Contents |
|--------|----------|
| A | ID (a unique number for each row) |
| B | Primary PDF link |
| C | Backup PDF link (optional - leave blank if none) |

### Step 2: Select Your Input Excel File

- Click the **Browse** button next to "Excel input file"
- Select the `.xlsx` file containing your list of links

### Step 3: Select a Download Folder

- Click the **Browse** button next to "Download folder"
- Choose (or create) a folder where the downloaded PDFs will be saved

### Step 4: Select a Results File Location

- Click the **Browse** button next to "Result Excel file"
- Choose where to save the results summary (this will be a new `.xlsx` file)
- This file will tell you which downloads succeeded, which failed, and why

### Step 5: Start the Download

- Click **Start Download**
- A progress bar will show how far along the process is
- This may take a while depending on how many files you're downloading
- When finished, a "Download complete!" message will appear

### Step 6: Check the Results

- Open the results Excel file to see:
  - Which files downloaded successfully
  - Which link was used (primary or backup)
  - The filename each PDF was saved as
  - Any error messages for failed downloads
- Downloaded PDFs will be in the folder you selected in Step 3

## Re-running After a Previous Download

The program remembers which files were already downloaded successfully. If you run it again:

- Already-downloaded files will be **skipped automatically** (marked as already downloaded in the results)
- Only new or previously failed records will be attempted again

This means you can safely re-run the program if some downloads fail the first time - it won't re-download files you already have.

## Starting Fresh (Reset)

If you want the program to forget all previous progress and re-check every file from scratch:

- Click the **Reset Progress (re-check all)** button
- Confirm when prompted
- The next download run will check every record again, even ones that succeeded before
