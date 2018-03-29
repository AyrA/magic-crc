# magic-crc

Tool to create magic crc32 sums

## How it works

A Magic CRC is a CRC32 sum that looks nice to humans. Examples are `0xFFFFFFFF` and `0x12345678`.
This is achieved by either changing 4 consecutive bytes in the stream or appending 4 bytes to the stream.
This makes it suitable for all file types that can either be freely extended or partially changed.
It's not possible to obtain the original values of the 4 overwritten bytes.

## Usage

A magic crc can be used to quickly spot errors in files.
It will not allow to correct errors and it will not protect against malicious changes.
The nain usage is to verify file transfers without the need to obtain the original checksum first.
There's a 1 in 4 billion chance that a change goes undetected.
Magic CRC sums have been used by Microsoft for their ISO images in the past.
The tool they used to author ISO images has a command line switch to do that.

## How to use

Command line: `magic-crc [/O offset] [/C CRC] <input> [output]`

The order of the arguments can be freely chosen, but the output file always comes after the input file.

### /O Offset

Specifies the offset of the bytes to change

**Optional**; If not specified, the 4 bytes are appended

A magic crc works by changing 4 consecutive bytes.
The offset plus the 4 bytes must fit completely inside the input file.
Negative numbers are allowed and assumed to be an offset from the end.

**Example**: `/O -4` will change the last 4 bytes of data. Useful for disk images

### /C CRC

Specifies the target CRC value

**Optional**; If not specified, `0xFFFFFFFF` is used

Any crc sum in the range of 0x00000000 to 0xFFFFFFFF is obtainable.
This argument specifies the new target CRC sum.

The prefix `0x` is optional but the number is always treated as hexadecimal

**Example**: `/C 12345678`

### input

**Required**

File whose CRC sum is to be changed.
It has to be at least 4 bytes in length.

### output

**Optional**; If not specified, defaults to the `input` argument.

Specifies the file where to write the new CRC sum to.
If this is not specified, the input file is changed in-place without completely rewrite it.
If this is specified, the input file is fully copied to the output file first and then the output is changed in-place
