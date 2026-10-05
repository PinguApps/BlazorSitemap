Feature: Independent demos remain buildable after generation
  Normal restores must keep using the demo's actual local NuGet packages.

  Scenario: Plain Debug and Release builds ignore incompatible ambient package copies and retain the generated version
    Given a fresh independent demo with a nondefault package version
    And an incompatible copy of that package exists in its ambient NuGet cache
    When the demo is built normally in "Debug"
    Then the demo build succeeds using its persistent local package cache
    When the demo is built normally in "Release"
    Then the demo build succeeds using its persistent local package cache
