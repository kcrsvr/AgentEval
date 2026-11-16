const scenarioExamples = {
    roundTrip: {
        pattern: "roundTrip",
        objective: "Assess whether the agent gathers the trip details it needs, confirms the itinerary, and gives a grounded roaming recommendation for a direct trip.",
        groundTruth: {
            pattern: "roundTrip",
            language: "English",
            customer: {
                initialMessage: "I'm travelling to India for a week. What roaming option would work best?",
                facts: {
                    travelOrder: [
                        { type: "land", country: "India", places: ["Delhi"], days: 7, position: "destination" }
                    ],
                    cruise: false,
                    returnsToCanada: true
                }
            },
            expectedBehavior: [
                "Ask one relevant missing-trip question at a time.",
                "Confirm the complete itinerary before requesting a recommendation.",
                "Use the approved roaming rates and recommendation tools; do not invent prices."
            ],
            verifiedExpectedRecommendation: null
        }
    },
    stopover: {
        pattern: "stopover",
        objective: "Assess whether the agent captures each country and its duration in travel order, confirms the full itinerary, and grounds its recommendation in the approved tools.",
        groundTruth: {
            pattern: "stopover",
            language: "English",
            customer: {
                initialMessage: "I'm flying from Canada to India with a two-day stop in Japan. What roaming option should I consider?",
                facts: {
                    travelOrder: [
                        { type: "land", country: "Japan", places: ["Tokyo"], days: 2, position: "stopover" },
                        { type: "land", country: "India", places: ["Delhi"], days: 8, position: "destination" }
                    ],
                    cruise: false,
                    returnsToCanada: true
                }
            },
            expectedBehavior: [
                "Retain the stopover and destination as separate ordered land segments.",
                "Ask only for missing itinerary details, then confirm the complete trip.",
                "Use approved rates and recommendation tools; do not invent prices or combine country rates."
            ],
            verifiedExpectedRecommendation: null
        }
    },
    cruise: {
        pattern: "landCruiseLand",
        objective: "Assess whether the agent gathers the land and cruise itinerary, cruise line and ship, checks eligibility, confirms the trip, and only then requests a tool-grounded recommendation.",
        groundTruth: {
            pattern: "landCruiseLand",
            language: "English",
            customer: {
                initialMessage: "I'm spending three days in Japan, then taking a cruise to Australia and New Zealand before staying two days in New Zealand. What roaming option could work for the trip?",
                facts: {
                    travelOrder: [
                        { type: "land", country: "Japan", places: ["Tokyo"], days: 3, position: "beforeCruise" },
                        {
                            type: "cruise",
                            cruiseLine: "Princess Cruises",
                            ship: "Diamond Princess",
                            daysTotal: 7,
                            timeline: [
                                { dayFrom: 1, dayTo: 1, type: "port", country: "Japan", place: "Yokohama", note: "boarding" },
                                { dayFrom: 2, dayTo: 3, type: "sea", days: 2 },
                                { dayFrom: 4, dayTo: 4, type: "port", country: "Australia", place: "Sydney" },
                                { dayFrom: 5, dayTo: 6, type: "sea", days: 2 },
                                { dayFrom: 7, dayTo: 7, type: "port", country: "New Zealand", place: "Auckland", note: "return" }
                            ],
                            eligibilityVerified: false
                        },
                        { type: "land", country: "New Zealand", places: ["Auckland"], days: 2, position: "afterCruise" }
                    ],
                    cruise: true,
                    returnsToCanada: true
                }
            },
            expectedBehavior: [
                "Preserve the land-before, cruise-timeline, land-after order and keep land days separate from cruise days.",
                "Ask for missing cruise itinerary details, line, or ship one question at a time; do not guess eligibility.",
                "Confirm the itinerary and obtain approved rates for the relevant destinations and cruise before recommending."
            ],
            verifiedExpectedRecommendation: null
        }
    }
};

document.querySelectorAll("[data-example]").forEach((button) => {
    button.addEventListener("click", () => {
        const example = scenarioExamples[button.dataset.example];
        if (!example) return;

        document.getElementById("travel-pattern").value = example.pattern;
        document.getElementById("Objective").value = example.objective;
        document.getElementById("GroundTruthJson").value = JSON.stringify(example.groundTruth, null, 2);

        document.querySelectorAll("[data-example]").forEach((item) => item.classList.remove("selected"));
        button.classList.add("selected");
    });
});
