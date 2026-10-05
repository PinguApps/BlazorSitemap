Feature: A Blazor application independently consumes the packed package
  The application and referenced Razor library use PackageReference, never the package project.
  Its Program.cs and Razor pages are executable documentation examples.

  Scenario: Compiled fixed localized dynamic and referenced-library pages work and render successfully
    Given the packed Blazor consumer
    When the sitemap is requested
    Then the sitemap contains exactly these canonical paths:
      | path                     |
      | /                        |
      | /auto                    |
      | /about                   |
      | /blog                    |
      | /en-gb/counter           |
      | /cy-gb/counter           |
      | /en-gb/blog/welcome      |
      | /cy-gb/blog/croeso       |
      | /en-gb/blog/the-coast    |
      | /library                 |
      | /api/guide               |
      | /api/articles/welcome    |
      | /api/articles/the-coast  |
    And the sitemap is valid UTF-8 XML with the protocol namespace and content type
    And every advertised page renders successfully
    And the alias still renders the About page
    And the excluded page renders noindex
    And every consumer URL has lastmod
    And lastmod for "/" matches source state "tests/Consumer/Components/Pages/Home.razor.sitemap.json"
    And lastmod for "/about" matches source state "tests/Consumer/Components/Pages/About.razor.sitemap.json"
    And lastmod for "/library" matches source state "tests/Consumer.Library/LibraryPage.razor.sitemap.json"
    And lastmod for "/auto" matches source state "tests/Consumer.Client/AutoPage.razor.sitemap.json"
    And lastmod for "/en-gb/blog/welcome" is "2026-09-01T10:00:00Z"
    And lastmod for "/cy-gb/blog/croeso" is "2026-09-02T11:00:00Z"
    And lastmod for "/api/guide" is "2026-09-01"
    And lastmod for "/api/articles/welcome" is "2026-09-01T10:00:00Z"

  Scenario: Article edits additions unpublication and deletion need no rebuild
    Given the packed Blazor consumer
    When the sitemap is requested
    And the demo publication operation is "edit"
    And the sitemap is requested
    Then lastmod for "/en-gb/blog/welcome" differs from "2026-09-01T10:00:00Z"
    And lastmod for "/blog" differs from "2026-09-04T13:00:00Z"
    When the demo publication operation is "add"
    And the sitemap is requested
    Then the sitemap contains 15 canonical URLs
    When the demo publication operation is "unpublish"
    And the sitemap is requested
    Then the sitemap contains 13 canonical URLs
    When the demo publication operation is "delete"
    And the sitemap is requested
    Then the sitemap contains 11 canonical URLs
    And every advertised page renders successfully

  Scenario: Normal configuration supports a mounted application
    Given the packed Blazor consumer
    And public base URL "https://public.example/portal"
    And the application path base is "/portal"
    When the sitemap is requested
    Then all canonical URLs start with "https://public.example/portal/"
    And every advertised page renders successfully
