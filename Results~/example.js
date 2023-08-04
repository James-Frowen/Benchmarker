const { CompareResults, PrintTResult } = require('./Compare');

// compare results from json files
const Results_1 = {
    title: "Testing idea 1",
    file: "Results-Player_2023-08-04_16-59-08.json",
};

const Results_2 = {
    title: "Testing idea 2",
    file: "Results-Player_2023-08-04_17-00-50.json",
}

CompareResults(Results_1, Results_2);


// compare results from a single method

const SingleMethods = [
    {
        name: "Method_First",
        methodTime: {
            mean: 687,
            stdDev: 786,
        }
    },
    {
        name: "Method_Second",
        methodTime: {
            mean: 390,
            stdDev: 620,
        }
    },
    {
        name: "Method_Third",
        methodTime: {
            mean: 363,
            stdDev: 598,
        }
    }
]

PrintTResult(SingleMethods[0], SingleMethods[1]);
PrintTResult(SingleMethods[0], SingleMethods[2]);
PrintTResult(SingleMethods[1], SingleMethods[2]);
