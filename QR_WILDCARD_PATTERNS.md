# QR Code Wildcard Pattern Matching

## Overview
The `QrValuesExcludingRedRemoval` list now supports wildcard patterns using the asterisk (`*`) symbol. This allows you to create flexible matching rules without listing every possible QR code value individually.

## Wildcard Syntax

### Asterisk (`*`)
The asterisk matches **one or more** characters at that position.

**Important**: The asterisk requires at least one character to match (it's not optional).

## Pattern Examples

### Example 1: Suffix Matching
Match all QR codes ending with `-FRTCVR`:
```json
{
  "QrValuesExcludingRedRemoval": ["*-FRTCVR"]
}
```

**Matches:**
- `APT24A-FRTCVR` ?
- `APT24B-FRTCVR` ?
- `TEST-FRTCVR` ?
- `XYZ123-FRTCVR` ?

**Does NOT match:**
- `-FRTCVR` ? (asterisk requires at least one character)
- `FRTCVR` ? (missing hyphen)
- `APT24A-FRTCVR-EXTRA` ? (has extra characters after)

### Example 2: Prefix Matching
Match all QR codes starting with `TEMPLATE-`:
```json
{
  "QrValuesExcludingRedRemoval": ["TEMPLATE-*"]
}
```

**Matches:**
- `TEMPLATE-A` ?
- `TEMPLATE-123` ?
- `TEMPLATE-MASTER` ?

**Does NOT match:**
- `TEMPLATE-` ? (asterisk requires at least one character)
- `MY-TEMPLATE-A` ? (doesn't start with TEMPLATE-)

### Example 3: Contains Pattern
Match all QR codes containing `-CLEAN-`:
```json
{
  "QrValuesExcludingRedRemoval": ["*-CLEAN-*"]
}
```

**Matches:**
- `APT-CLEAN-001` ?
- `TEST-CLEAN-VERSION2` ?
- `X-CLEAN-Y` ?

**Does NOT match:**
- `-CLEAN-` ? (asterisks require at least one character each)
- `CLEAN` ? (missing hyphens and surrounding text)
- `APT-CLEAN` ? (missing trailing hyphen and text)

### Example 4: Multiple Wildcards
Match complex patterns:
```json
{
  "QrValuesExcludingRedRemoval": ["APT*-*COVER"]
}
```

**Matches:**
- `APT24A-FRTCOVER` ?
- `APT24B-BACKCOVER` ?
- `APTXYZ-TESTCOVER` ?

**Does NOT match:**
- `APT-COVER` ? (first asterisk requires at least one character)
- `APT24-COVER` ? (second asterisk requires at least one character)
- `XYZ-FRTCOVER` ? (doesn't start with APT)

### Example 5: Exact Match (No Wildcards)
You can still use exact matches without wildcards:
```json
{
  "QrValuesExcludingRedRemoval": ["CLEAN", "NO_RED_INK"]
}
```

**Matches:**
- `CLEAN` ? (exact match, case-insensitive)
- `NO_RED_INK` ? (exact match, case-insensitive)

**Does NOT match:**
- `CLEAN-VERSION` ?
- `MY-CLEAN` ?
- `NO_RED` ?

### Example 6: Mixed Patterns
Combine wildcards and exact matches:
```json
{
  "QrValuesExcludingRedRemoval": [
    "*-FRTCVR",
    "*-BACKCOVER", 
    "CLEAN",
    "TEMPLATE-*"
  ]
}
```

This will exclude QR codes that:
- End with `-FRTCVR` (any prefix)
- End with `-BACKCOVER` (any prefix)
- Exactly match `CLEAN`
- Start with `TEMPLATE-` (any suffix)

## Configuration

### Current Configuration (WPF & Console)
```json
{
  "QrScanner": {
    "QrValuesExcludingRedRemoval": [ "*-FRTCVR", "CLEAN" ]
  }
}
```

This configuration will skip red pixel removal for:
- Any QR code ending with `-FRTCVR` (e.g., `APT24A-FRTCVR`, `APT24B-FRTCVR`)
- Any QR code exactly matching `CLEAN`

### Recommended Patterns

#### For Cover Pages
```json
{
  "QrValuesExcludingRedRemoval": [
    "*-FRTCVR",
    "*-BACKCOVER",
    "*-COVER"
  ]
}
```

#### For Template/Clean Pages
```json
{
  "QrValuesExcludingRedRemoval": [
    "TEMPLATE*",
    "CLEAN*",
    "*-CLEAN",
    "NO_RED_INK"
  ]
}
```

#### For Specific Test Types
```json
{
  "QrValuesExcludingRedRemoval": [
    "MULTIPLE_CHOICE-*",
    "MC-*",
    "*-SCANTRON",
    "BUBBLE_SHEET"
  ]
}
```

## Case Sensitivity

**Wildcard matching is case-insensitive by default.**

Pattern: `*-FRTCVR`

**All of these match:**
- `APT24A-FRTCVR` ?
- `apt24a-frtcvr` ?
- `APT24A-frtcvr` ?
- `apt24a-FRTCVR` ?

## Technical Details

### Pattern Conversion
Internally, wildcard patterns are converted to regular expressions:

| Wildcard Pattern | Regex Pattern | Description |
|------------------|---------------|-------------|
| `*-FRTCVR` | `^.*-FRTCVR$` | Starts with one+ chars, ends with `-FRTCVR` |
| `TEMPLATE-*` | `^TEMPLATE-.*$` | Starts with `TEMPLATE-`, ends with one+ chars |
| `*-CLEAN-*` | `^.*-CLEAN-.*$` | Contains `-CLEAN-` with chars on both sides |
| `CLEAN` | `^CLEAN$` | Exact match only |

### Special Characters
The following characters are treated as literals (not regex):
- `.` (period)
- `+` (plus)
- `?` (question mark)
- `[` `]` (brackets)
- `{` `}` (braces)
- `(` `)` (parentheses)
- `|` (pipe)
- `^` (caret)
- `$` (dollar sign)
- `\` (backslash)

Only `*` is treated specially as a wildcard.

### Performance
- Pattern matching uses compiled regular expressions
- Patterns are evaluated in order until a match is found
- First match determines the result
- Keep patterns specific to optimize performance

## Testing Examples

### Test Scenario 1: Front Cover Pages
**Configuration:**
```json
{
  "QrValuesExcludingRedRemoval": ["*-FRTCVR"]
}
```

**Test Cases:**
| QR Code | Matches Pattern? | Red Removal Applied? |
|---------|------------------|---------------------|
| `APT24A-FRTCVR` | Yes | No (skipped) |
| `APT24B-FRTCVR` | Yes | No (skipped) |
| `APT24A-CONTENT` | No | Yes (applied) |
| `FRTCVR` | No | Yes (applied) |

### Test Scenario 2: Multiple Patterns
**Configuration:**
```json
{
  "QrValuesExcludingRedRemoval": [
    "*-FRTCVR",
    "*-BACKCOVER",
    "CLEAN"
  ]
}
```

**Test Cases:**
| QR Code | Matches Pattern? | Which Pattern? | Red Removal Applied? |
|---------|------------------|----------------|---------------------|
| `APT24A-FRTCVR` | Yes | `*-FRTCVR` | No (skipped) |
| `APT24B-BACKCOVER` | Yes | `*-BACKCOVER` | No (skipped) |
| `CLEAN` | Yes | `CLEAN` | No (skipped) |
| `TEST-CLEAN` | No | None | Yes (applied) |
| `APT24A-CONTENT` | No | None | Yes (applied) |

### Test Scenario 3: Prefix Matching
**Configuration:**
```json
{
  "QrValuesExcludingRedRemoval": ["TEMPLATE-*"]
}
```

**Test Cases:**
| QR Code | Matches Pattern? | Red Removal Applied? |
|---------|------------------|---------------------|
| `TEMPLATE-A` | Yes | No (skipped) |
| `TEMPLATE-MASTER` | Yes | No (skipped) |
| `MY-TEMPLATE-A` | No | Yes (applied) |
| `TEMPLATE` | No | Yes (applied) |

## Console Output Examples

### Match Found
```
Page 1: QR code detected: APT24A-FRTCVR
Page 1: QR code matches exclusion criteria - skipping red pixel removal
```

### No Match
```
Page 2: QR code detected: APT24A-CONTENT
Page 2: QR code does not match exclusion criteria - applying red pixel removal
```

### Pattern Matching Details (Debug)
When debugging, you can see which pattern matched:
```
Page 1: QR code detected: APT24A-FRTCVR
Page 1: Checking pattern '*-FRTCVR': MATCH
Page 1: QR code matches exclusion criteria - skipping red pixel removal
```

## Migration from Exact Matches

### Before (Exact Matches Only)
```json
{
  "QrValuesExcludingRedRemoval": [
    "APT24A-FRTCVR",
    "APT24B-FRTCVR",
    "APT24C-FRTCVR",
    "APT24D-FRTCVR"
  ]
}
```

### After (With Wildcards)
```json
{
  "QrValuesExcludingRedRemoval": [
    "*-FRTCVR"
  ]
}
```

**Benefits:**
- Shorter configuration
- Automatically handles new codes with same suffix
- Easier to maintain
- More flexible

## Best Practices

### 1. Be Specific
Use specific patterns to avoid unintended matches:
- ? Good: `*-FRTCVR` (specific suffix)
- ? Bad: `*CVR` (too generic, might match unintended codes)

### 2. Test Patterns
Always test with sample QR codes:
```
Test codes: APT24A-FRTCVR, APT24B-FRTCVR, TEST-FRTCVR
Pattern: *-FRTCVR
Expected: All should match ?
```

### 3. Order Doesn't Matter
Patterns are checked in order, but first match wins:
```json
{
  "QrValuesExcludingRedRemoval": [
    "CLEAN",           // Check exact match first (faster)
    "*-FRTCVR",        // Then wildcards
    "*-BACKCOVER"
  ]
}
```

### 4. Document Your Patterns
Add comments (if supported) or maintain separate documentation:
```json
{
  "QrValuesExcludingRedRemoval": [
    "*-FRTCVR",      // Front cover pages
    "*-BACKCOVER",   // Back cover pages
    "CLEAN",         // Clean template pages
    "TEMPLATE-*"     // All template variations
  ]
}
```

### 5. Use Consistent Naming
Design your QR code scheme with wildcards in mind:
- Good: `TYPE-VARIANT` (e.g., `APT24A-FRTCVR`, `APT24B-FRTCVR`)
- Allows pattern: `*-FRTCVR`

### 6. Avoid Overly Generic Patterns
```json
// Too generic - matches almost everything
"*"  ?

// Better - specific enough
"*-COVER"  ?
```

## Troubleshooting

### Pattern Not Matching

**Problem**: QR code should match but doesn't

**Check:**
1. Verify exact spelling (case doesn't matter, but characters do)
2. Check for extra spaces or special characters
3. Ensure asterisk placement is correct
4. Test pattern with console output

**Example:**
```
Pattern: *-FRTCVR
QR Code: APT24A -FRTCVR  (has space before hyphen)
Result: NO MATCH ?
```

### Unintended Matches

**Problem**: Pattern matches QR codes it shouldn't

**Check:**
1. Pattern might be too generic
2. Use more specific prefix/suffix
3. Add more context to pattern

**Example:**
```
Pattern: *-CVR  (too generic)
Unintended match: APT24A-DISCOVER ?

Better pattern: *-FRTCVR  (more specific)
Correct match: APT24A-FRTCVR ?
```

### Multiple Patterns Conflicting

**Problem**: Unsure which pattern is matching

**Solution**: Check console output - it shows when match occurs

### Case Issues

**Problem**: Worried about case sensitivity

**Solution**: Don't worry - all matching is case-insensitive by default

## Advanced Patterns

### Combining Multiple Wildcards
```json
{
  "QrValuesExcludingRedRemoval": [
    "APT*-*COVER"    // APT + anything + hyphen + anything + COVER
  ]
}
```

**Matches:**
- `APT24A-FRTCOVER` ?
- `APTXYZ-BACKCOVER` ?
- `APT123-TESTCOVER` ?

### Pattern Alternatives
Instead of using wildcards, consider these alternatives:

**Option 1: Multiple specific patterns**
```json
{
  "QrValuesExcludingRedRemoval": ["*-FRTCVR", "*-BACKCOVER", "*-COVER"]
}
```

**Option 2: Single wildcard (if naming is consistent)**
```json
{
  "QrValuesExcludingRedRemoval": ["*COVER"]  // Anything ending with COVER
}
```

## Summary

**Wildcard support allows:**
- ? Flexible pattern matching
- ? Shorter configuration files
- ? Automatic handling of new QR code variations
- ? Case-insensitive matching
- ? Multiple wildcards per pattern

**Remember:**
- `*` matches one or more characters (not zero)
- Patterns are case-insensitive
- First match wins
- Be specific to avoid unintended matches
- Test patterns with sample data

## Implementation Details

### Code Changes
The wildcard matching is implemented in `BookletProcessorService.cs`:

```csharp
private static bool MatchesWildcard(string value, string pattern, bool ignoreCase = true)
{
    // Convert wildcard pattern to regex
    var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
    var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
    return Regex.IsMatch(value, regexPattern, options);
}
```

### Usage in Code
```csharp
var qrMatchesExclusionList = _qrValuesExcludingRedRemoval.Any(pattern =>
    MatchesWildcard(qrCodeValue, pattern, ignoreCase: true));
```

This replaces the old substring matching logic and provides full wildcard support.
