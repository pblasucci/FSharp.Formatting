﻿// --------------------------------------------------------------------------------------
// F# Markdown (StringParsing.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Patterns

open System
open FSharp.Collections

// --------------------------------------------------------------------------------------
// Active patterns that simplify parsing of strings and lists of strings (lines)
// --------------------------------------------------------------------------------------

module String =
  /// Matches when a string is a whitespace or null
  let (|WhiteSpace|_|) s = 
    if String.IsNullOrWhiteSpace(s) then Some() else None

  /// Matches when a string does starts with non-whitespace
  let (|Unindented|_|) (s:string) = 
    if not (String.IsNullOrWhiteSpace(s)) && s.TrimStart() = s then Some() else None

  /// Returns a string trimmed from both start and end
  let (|TrimBoth|) (text:string) = text.Trim()
  /// Returns a string trimmed from the end
  let (|TrimEnd|) (text:string) = text.TrimEnd()
  /// Returns a string trimmed from the start
  let (|TrimStart|) (text:string) = text.TrimStart()

  /// Retrusn a string trimmed from the end using characters given as a parameter
  let (|TrimEndUsing|) chars (text:string) = text.TrimEnd(Array.ofSeq chars)

  /// Returns a string trimmed from the start together with 
  /// the number of skipped whitespace characters
  let (|TrimStartAndCount|) (text:string) = 
    let trimmed = text.TrimStart([|' '; '\t'|])
    let len = text.Length - trimmed.Length
    len, text.Substring(0, len).Replace("\t", "    ").Length, trimmed

  /// Matches when a string starts with any of the specified sub-strings
  let (|StartsWithAny|_|) (starts:seq<string>) (text:string) = 
    if starts |> Seq.exists (text.StartsWith) then Some() else None
  /// Matches when a string starts with the specified sub-string
  let (|StartsWith|_|) (start:string) (text:string) = 
    if text.StartsWith(start) then Some(text.Substring(start.Length)) else None
  /// Matches when a string starts with the specified sub-string
  /// The matched string is trimmed from all whitespace.
  let (|StartsWithTrim|_|) (start:string) (text:string) = 
    if text.StartsWith(start) then Some(text.Substring(start.Length).Trim()) else None

  /// Matches when a string starts with the given value and ends 
  /// with a given value (and returns the rest of it)
  let (|StartsAndEndsWith|_|) (starts, ends) (s:string) =
    if s.StartsWith(starts) && s.EndsWith(ends) && 
       s.Length >= starts.Length + ends.Length then 
      Some(s.Substring(starts.Length, s.Length - starts.Length - ends.Length))
    else None

  /// Matches when a string starts with the given value and ends 
  /// with a given value (and returns trimmed body)
  let (|StartsAndEndsWithTrim|_|) args = function
    | StartsAndEndsWith args (TrimBoth res) -> Some res
    | _ -> None

  /// Matches when a string starts with a non-zero number of complete
  /// repetitions of the specified parameter (and returns the number
  /// of repetitions, together with the rest of the string)
  ///
  ///    let (StartsWithRepeated "/\" (2, " abc")) = "/\/\ abc"
  ///
  let (|StartsWithRepeated|_|) (repeated:string) (text:string) = 
    let rec loop i = 
      if i = text.Length then i
      elif text.[i] <> repeated.[i % repeated.Length] then i
      else loop (i + 1)

    let n = loop 0 
    if n = 0 || n % repeated.Length <> 0 then None
    else Some(n/repeated.Length, text.Substring(n, text.Length - n)) 

  /// Matches when a string starts with a sub-string wrapped using the 
  /// opening and closing sub-string specified in the parameter.
  /// For example "[aa]bc" is wrapped in [ and ] pair. Returns the wrapped
  /// text together with the rest.
  let (|StartsWithWrapped|_|) (starts:string, ends:string) (text:string) = 
    if text.StartsWith(starts) then 
      let id = text.IndexOf(ends, starts.Length)
      if id >= 0 then 
        let wrapped = text.Substring(starts.Length, id - starts.Length)
        let rest = text.Substring(id + ends.Length, text.Length - id - ends.Length)
        Some(wrapped, rest)
      else None
    else None

  /// Matches when a string consists of some number of 
  /// complete repetitions of a specified sub-string.
  let (|EqualsRepeated|_|) repeated = function
    | StartsWithRepeated repeated (n, "") -> Some()
    | _ -> None 

  /// Given a list of lines indented with certan number of whitespace 
  /// characters (spaces), remove the spaces from the beginning of each line 
  /// and return the string as a list of lines
  let removeSpaces lines =
    let spaces =
      lines 
      |> Seq.filter (String.IsNullOrWhiteSpace >> not)
      |> Seq.map (fun line -> line |> Seq.takeWhile Char.IsWhiteSpace |> Seq.length)
      |> fun xs -> if Seq.isEmpty xs then 0 else Seq.min xs
    lines 
    |> Seq.map (fun line -> 
        if String.IsNullOrWhiteSpace(line) then ""
        else line.Substring(spaces))


module List =
  /// Matches a list if it starts with a sub-list that is delimited
  /// using the specified delimiters. Returns a wrapped list and the rest.
  let inline (|DelimitedWith|_|) startl endl input = 
    if List.startsWith startl input then
      match List.partitionUntilEquals endl (List.skip startl.Length input) with 
      | Some(pre, post) -> Some(pre, List.skip endl.Length post)
      | None -> None
    else None

  /// Matches a list if it starts with a sub-list. Returns the list.
  let inline (|StartsWith|_|) startl input = 
    if List.startsWith startl input then Some input else None

  /// Matches a list if it starts with a sub-list that is delimited
  /// using the specified delimiter. Returns a wrapped list and the rest.
  let inline (|Delimited|_|) str = (|DelimitedWith|_|) str str

  /// Matches a list if it starts with a bracketed list. Nested brackets
  /// are skipped (by counting opening and closing brackets) and can be 
  /// escaped using the '\' symbol.
  let (|BracketDelimited|_|) startc endc input =
    let rec loop acc count = function
      | '\\'::x::xs when x = endc -> loop (x::acc) count xs
      | x::xs when x = endc && count = 0 -> Some(List.rev acc, xs)
      | x::xs when x = endc -> loop (x::acc) (count - 1) xs
      | x::xs when x = startc -> loop (x::acc) (count + 1) xs
      | x::xs -> loop (x::acc) count xs
      | [] -> None
    match input with
    | x::xs when x = startc -> loop [] 0 xs
    | _ -> None

  /// Returns a list of characters as a string.
  let (|AsString|) chars = String(Array.ofList chars)

module Lines = 
  /// Removes blank lines from the start and the end of a list
  let (|TrimBlank|) lines = 
    lines
    |> List.skipWhile String.IsNullOrWhiteSpace |> List.rev
    |> List.skipWhile String.IsNullOrWhiteSpace |> List.rev

  /// Matches when there are some lines at the beginning that are 
  /// either empty (or whitespace) or start with the specified string.
  /// Returns all such lines from the beginning until a different line.
  let (|TakeStartingWithOrBlank|_|) start input = 
    match List.partitionWhile (fun s -> 
            String.IsNullOrWhiteSpace s || s.StartsWith(start)) input with
    | matching, rest when matching <> [] -> Some(matching, rest)
    | _ -> None

  /// Matches when there are some lines at the beginning that are 
  /// either empty (or whitespace) or start with at least 4 spaces (a tab counts as 4 spaces here).
  /// Returns all such lines from the beginning until a different line and 
  /// the number of spaces the first line started with.
  let (|TakeCodeBlock|_|) (input:string list) =
    let spaceNum =
      match input with
      | h :: _ ->
        let head = (input |> List.head).Replace("\t", "    ") |> Seq.toList
        let spaces, _ = List.partitionWhile (fun s -> s = ' ') head
        spaces.Length
      | _ -> 0
    let startsWithSpaces (s:string) =
      let normalized = s.Replace("\t", "    ")
      normalized.Length >= spaceNum &&
        normalized.Substring(0, spaceNum) = System.String(' ', spaceNum)
    match List.partitionWhile (fun s -> 
            String.IsNullOrWhiteSpace s || startsWithSpaces s) input with
    | matching, rest when matching <> [] && spaceNum >= 4 ->
      Some(spaceNum, matching, rest)
    | _ -> None

  /// Removes whitespace lines from the beginning of the list
  let (|TrimBlankStart|) = List.skipWhile (String.IsNullOrWhiteSpace)

  /// Trims all lines of the current paragraph
  let (|TrimParagraphLines|) lines =
    lines
    // first remove all whitespace on the beginning of the line
    |> List.map (fun (s:string) -> s.TrimStart())
    // Now remove all additional spaces at the end, but keep two spaces if existent
    |> List.map (fun s ->
      let endsWithTwoSpaces = s.EndsWith("  ")
      s.TrimEnd([|' '|]) + if endsWithTwoSpaces then "  " else "")

/// Parameterized pattern that assigns the specified value to the 
/// first component of a tuple. Usage:
///
///    match str with
///    | Let 1 (n, "one") | Let 2 (n, "two") -> n
/// 
let (|Let|) a b = (a, b)

open System.Collections.Generic

/// Utility for parsing commands. Commands can be used in different places. We 
/// recognize `key1=value, key2=value` and also `key1:value, key2:value`
/// The key of the command should be identifier with just 
/// characters in it - otherwise, the parsing fails.
let (|ParseCommands|_|) (str:string) = 
  let kvs = 
    [ for cmd in str.Split(',') do
        let kv = cmd.Split([| '='; ':' |])
        if kv.Length = 2 then yield kv.[0].Trim(), kv.[1].Trim()
        elif kv.Length = 1 then yield kv.[0].Trim(), "" ] 
  let allKeysValid = 
    kvs |> Seq.forall (fst >> Seq.forall (fun c -> Char.IsLetter c || c = '_' || c = '-'))
  if allKeysValid && kvs <> [] then Some(dict kvs) else None

/// Utility for parsing commands - this deals with a single command.
/// The key of the command should be identifier with just 
/// characters in it - otherwise, the parsing fails.
let (|ParseCommand|_|) (cmd:string) = 
  let kv = cmd.Split([| '='; ':' |])
  if kv.Length >= 1 && not (Seq.forall Char.IsLetter kv.[0]) then None
  elif kv.Length = 2 then Some(kv.[0].Trim(), kv.[1].Trim())
  elif kv.Length = 1 then Some(kv.[0].Trim(), "")
  else None
  
/// Lookup in a dictionary
let (|Command|_|) k (d:IDictionary<_, _>) =
  match d.TryGetValue(k) with
  | true, v -> Some v
  | _ -> None 
