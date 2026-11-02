# Third-party notices

Spindle's own code is GPLv3 (LICENSE). Some of Hymie's decoders are ported
from someone else's work: each is the same decoder, rewritten function for
function in C#, at the upstream commit given. Their license texts are in
licenses/.

- **minimp3** (commit `ea99364`), by lieff: the MP3 decoder's core, Layer III,
  ported to C# with its tables copied value for value
  (src/Spindle.Codecs/Mp3.cs, Mp3Tables.cs).
  License: CC0 1.0 Universal (dedicated to the public domain).
  Source: https://github.com/lieff/minimp3

- **FAAD2** (commit `521d105`): the AAC decoder's core, its AAC-LC path,
  ported to C# with its tables copied value for value
  (src/Spindle.Codecs/Aac.cs, AacTables.cs). Code from FAAD2 is copyright
  (c) Nero AG, www.nero.com.
  License: GNU GPL, version 2 or later.
  Source: https://github.com/knik0/faad2

- **Apple Lossless (ALAC)** (commit `c38887c`), by Apple: the ALAC decoder's
  core: its adaptive Golomb decoder, dynamic predictor, stereo unmix and
  channel elements, ported to C# (src/Spindle.Codecs/Alac.cs, which says
  what was changed). Copyright (c) 2011 Apple Inc.
  License: Apache License 2.0.
  Source: https://github.com/macosforge/alac

Each app that compiles Spindle in names these in its own
THIRD-PARTY-NOTICES.md and carries the same license texts.
