# Benchmarker

Benchmarker for methods in unity. Add attribute to methods and run your game, Benchmarker will measure and collect time it takes for those methods to run. Results will then be given in a markdown table.

## Requires 
- Mono.cecil
- Mirage (for IL helper methods)

## Install 

Import all files into project

## How to use 

1) Add `[BenchmarkMethod("name")]` (and optionally `[BenchmarkCategory("category")]`) to methods you want to measure.
2) Call `BenchmarkRunner.StartRecording(300, true, true);`
3) Run game code with methods you are measuring
4) Look at markdown file for results
