Feature: Runtime publication and honest freshness

  Background:
    Given an included page with route "/{locale}/blog/{slug}"
    And the page enables hreflang
    And the page uses a runtime provider
    And English and Welsh locales with English x-default

  Scenario: Only published available translations are advertised with their own timestamps
    Given published route instances:
      | locale | slug      | group | lastmod                   | published |
      | en-gb  | welcome   | one   | 2026-09-01T10:00:00Z       | true      |
      | cy-gb  | croeso    | one   | 2026-09-02T12:30:00+01:00  | true      |
      | en-gb  | english   | two   | 2026-09-03                | true      |
      | cy-gb  | draft     | two   | 2026-09-04                | false     |
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path                |
      | /en-gb/blog/welcome |
      | /cy-gb/blog/croeso  |
      | /en-gb/blog/english |
    And lastmod for "/en-gb/blog/welcome" is "2026-09-01T10:00:00Z"
    And lastmod for "/cy-gb/blog/croeso" is "2026-09-02T12:30:00+01:00"
    And lastmod for "/en-gb/blog/english" is "2026-09-03"
    And entry "/en-gb/blog/english" has 2 alternate links
    And entry "/cy-gb/blog/croeso" has 3 alternate links

  Scenario: A Welsh-only translation does not invent an English fallback
    Given published route instances:
      | locale | slug   | group | lastmod |
      | cy-gb  | croeso | one   | 2026-09-01 |
    When the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And entry "/cy-gb/blog/croeso" has 1 alternate links
    And lastmod for "/cy-gb/blog/croeso" is "2026-09-01"

  Scenario: Localized provider entries require an explicit equivalence identity
    Given published route instances:
      | locale | slug    | lastmod |
      | en-gb  | welcome | 2026-09-01 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "require AlternateGroup"

  Scenario: Unregistered locale values fail clearly
    Given published route instances:
      | locale | slug    | group | lastmod |
      | fr-fr  | welcome | one   | 2026-09-01 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "not a registered value"

  Scenario: Missing required values fail clearly
    Given published route instances:
      | locale | group | lastmod |
      | en-gb  | one   | 2026-09-01 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "missing parameter 'slug'"

  Scenario: Unknown provider values fail clearly
    Given published route instances:
      | locale | slug    | typo | group | lastmod |
      | en-gb  | welcome | x    | one   | 2026-09-01 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "unknown route parameter 'typo'"

  Scenario: Multiple URLs for a language within a group are ambiguous
    Given published route instances:
      | locale | slug    | group | lastmod |
      | en-gb  | welcome | one   | 2026-09-01 |
      | en-gb  | alias   | one   | 2026-09-01 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "multiple URLs for the same language"

  Scenario: Identical rows deduplicate and provider ordering cannot change XML
    Given published route instances:
      | locale | slug | group | lastmod    |
      | en-gb  | b    | b     | 2026-09-01 |
      | en-gb  | a    | a     | 2026-09-02 |
      | en-gb  | b    | b     | 2026-09-01 |
    When the sitemap is requested
    Then the sitemap contains 2 canonical URLs
    When provider order is reversed
    And the sitemap cache is invalidated
    And the sitemap is requested
    Then the sitemap XML is unchanged

  Scenario: Duplicate URLs with conflicting timestamps fail
    Given published route instances:
      | locale | slug | group | lastmod    |
      | en-gb  | a    | a     | 2026-09-01 |
      | en-gb  | a    | a     | 2026-09-02 |
    When the sitemap is requested
    Then generation fails with diagnostic containing "Conflicting sitemap"

  Scenario: Strict freshness fails rather than manufacturing a date
    Given lastmod is required
    And published route instances:
      | locale | slug | group | lastmod |
      | en-gb  | a    | a     | <none> |
    When the sitemap is requested
    Then generation fails with diagnostic containing "Missing trustworthy lastmod"

  Scenario: Edits and deletion become visible immediately after explicit invalidation
    Given published route instances:
      | locale | slug | group | lastmod    |
      | en-gb  | a    | a     | 2026-09-01 |
      | cy-gb  | a-cy | a     | 2026-09-02 |
    When the sitemap is requested
    And published instance 0 is updated to "2026-10-01T10:00:00Z"
    And published instance 1 is deleted
    And the sitemap cache is invalidated
    And the sitemap is requested
    Then the sitemap contains 1 canonical URLs
    And lastmod for "/en-gb/blog/a" is "2026-10-01T10:00:00Z"
    And entry "/en-gb/blog/a" has 2 alternate links

  Scenario: Runtime freshness is mandatory even without strict static freshness
    Given published route instances:
      | locale | slug | group | lastmod |
      | en-gb | a | a | <none> |
    When the sitemap is requested
    Then generation fails with diagnostic containing "Dynamic content requires a supplied timestamp"
