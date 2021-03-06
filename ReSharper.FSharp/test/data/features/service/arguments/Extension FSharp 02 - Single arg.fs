module Mod

open System.IO

[<AutoOpen>]
module FileExt =
    type FileInfo with
        member this.CreateDirectory (safe: bool) =
            Directory.CreateDirectory this.Directory.FullName

let x = FileInfo "abc.txt"
{selstart}x.CreateDirectory (true){selend}
