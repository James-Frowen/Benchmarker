const fs = require('fs');

function readJSONFile(filePath) {
    const fileData = fs.readFileSync(filePath, 'utf8');
    return JSON.parse(fileData);
}

function GetAllResults(data) {
    const results = [];
    const frameCount = parseInt(data.MetaData[0].slice("FrameCount:".length));
    data.Categories.forEach(cat => {
        cat.ProcessedResults.forEach(result => {
            results.push({
                name: result.benchmark.name,
                frameCount: frameCount,
                methodTime: {
                    mean: result.methodTime.mean,
                    stdDev: result.methodTime.stdDev,
                },
                count: {
                    mean: result.count.mean,
                    stdDev: result.count.stdDev,
                },
                frameTime: {
                    mean: result.frameTime.mean,
                    stdDev: result.frameTime.stdDev,
                },
            });
        });
    });
    return results;
}

function calculateTStatistic(a, b, a_b, b_n) {
    const numerator = a.mean - b.mean;
    const denominator = Math.sqrt((a.stdDev ** 2 / a_b) + (b.stdDev ** 2 / b_n));
    return numerator / denominator;
}

function CompareResults(A, B) {
    const resultA = GetAllResults(readJSONFile(A.file));
    const resultB = GetAllResults(readJSONFile(B.file));

    resultA.forEach(result => {
        const matchingResult = resultB.find(r => r.name === result.name);
        if (matchingResult) {
            PrintTResult(result, matchingResult, A.title, B.title);
        } else {
            console.log(`Warning: No matching result found for ${result.name} in file ${B.title}\n`);
        }
    });

    resultB.forEach(result => {
        const matchingResult = resultA.find(r => r.name === result.name);
        if (!matchingResult) {
            console.log(`Warning: No matching result found for ${result.name} in file ${A.title}\n`);
        }
    });
}

function PrintTResult(A, B, aTitle = 'A', bTitle = 'B') {
    const tStatistic = calculateTStatistic(A.methodTime, B.methodTime, A.frameCount, B.frameCount);

    const timeDifference = Math.abs(A.methodTime.mean - B.methodTime.mean);

    // if A is 100ms
    // and B is 200ms
    // then A is 100% faster than B
    // 100 => (slower - faster)) / faster * 100 ??;

    let faster;
    let percentFaster;
    if (A.methodTime.mean < B.methodTime.mean) {
        faster = aTitle;
        percentFaster = ((timeDifference / A.methodTime.mean) * 100).toFixed(2);
    }
    else {
        faster = bTitle;
        percentFaster = ((timeDifference / B.methodTime.mean) * 100).toFixed(2);
    }


    let title = `${A.name}`;
    if (A.name != B.name) {
        title += ` vs ${B.name}`;
    }

    console.log(title);
    console.log(`${faster} is ${percentFaster}% faster than the other`);
    console.log(`T-Statistic: ${Math.abs(tStatistic.toFixed(2))}\n`);
}

module.exports = {
    CompareResults,
    PrintTResult
};
