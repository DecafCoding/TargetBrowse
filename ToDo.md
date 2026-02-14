## Form Consistancy
Left-side form uniformity. sm vs 

In the /services/Utilities/FormatHelper.cs class Can you modify the FormatDatDisplay use proper singular and plural descriptions for Days, weeks, months, and years?

## Filter/Sort on Suggestions page
On the suggestions page/feature, there is a "High Priority" option in the filter dropdown. Can you place the value on the 


and sort on the suggestion pages does not appear to function. 


## All video titles
The video titles on the topics videos page (topics/{id}), channel-videos, and video-library page:
1.) Should link to watch page, remove any icons added at end of title.
2.) Should use "@((MarkupString)WebUtility.H remove tmlDecode(Suggestion.Video.Title))" to display titles

Remove the "Watch" button from the video-library video cards. This is redundant after the video title links to watch page.

## suggestions
Create Filtering down to individual topic (not sure about channel)
The "total" for suggestions should be like "Showing 42 of 120"

## channel-videos
Does the page pull from the db first and then if the LastCheckedDate is old enough it checks API?

## video-search
Change search box text to "Enter search term"
Make 4 video cards per row instead of 3

## Video Library
Move Search button to right side of search box. Make button sold blue primary.

## re-suggestions.
New suggestions should only happen if the reason is different. Like a new topic was added.

## Topic Onboarding
Should add all videos found to the video table
Should start with 100 videos and narrow down to the 50 most relevant

## Topics Page:
Add "Check Frequency" to Topics table

## Suggestion Feedback:
Track which topic-based suggestions users approve most

## Smart Topic Suggestions:
Suggest new topics based on approved suggestion patterns

## BUGS
Fix video Title with special characters: You don&#39;t need a Raspberry Pi! (Getting started with Microcontrollers)

## Summarizing videos:
Short Summary should first answer the title (like click bait or "this")

## Constants / Settings
Make all the constants (SuggestionRespository and SuggestionService) AppSettings

## Scoring.
by moving 1 point from Channels to Topics. So Channels 5 points and Topic 3 points.
Remove Maximum Pending Suggestions.

## Buttons and Styles
Save to library (blue solid, circle plus icon, "Library" text) bookmark-star, box, magic, pin-angle
Watch on YouTube ("YouTube" text, external link icon) box-arrow-up-right
Skip (eye-slash, hand-thumbs-down
Mark Watched
Rating - fire, battery, flask, gem, lightening, rocket

Probably remove "Play" button and instead link thumbnail and title to watch.

Suggestions
Approve (solid green), Pass (pink outline), Play (blue outline)

# Video-Library
Mark Watched (green outline)
Skip (red outline)
Watch (gray outline)
Add to Project (blue outline)

Topics/{id}/Videos
Save to Library (solid blue, full width)

Video-Search
Save to Library (solid blue, full width), Watch on YouTube (outline gray, full width)

## YT-026
Title: AI-Powered Thumbnail Analysis for Enhanced Suggestions

## Watch
When the transcript is being retrieved, a spinner is active on the Transcript button. Perhaps there are steps or a "Check" count could be displayed.

## Cleanup
FormatHelper in servicers should be in utilities
Shared Services folder cleanup
 - All Data based services in an area (maybe separated by entity type)
 - Have External Services area
 - RatingServiceBase not sure this is organized best (lots of abstract classes)

## 'TargetBrowse.Features.Projects.Components.ProjectGuideDisplay'
REmove the CSS inside the rasor page








