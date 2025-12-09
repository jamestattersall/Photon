

const ms = 24 * 60 * 60 * 1000; //milliseconds in a day

class Graphics {
    constructor(dateSeries = []) {
        this.dateSeries = dateSeries
        this.zoomFraction = 1.0
        this.plotPoints = dateSeries.length
        this.trueZero = false
        this.plot()
    }

    reset() {
        this.plotPath = "",
            this.yCaptions = [],
            this.xCaptions1 = [],
            this.xCaptions2 = []
    }


    plot() {
        this.reset()
        if (this.dateSeries == null || this.dateSeries.length < 2) return

        const svgHeight = 30
        const svgWidth = 65

        let ds
        if (this.zoomFraction >= 1) {
            ds = this.dateSeries
        } else {
            ds = this.dateSeries.slice(Math.floor(this.dateSeries.length * (1 - this.zoomFraction)))
                  
        }
        this.plotPoints = ds.length;

        let daySeries = ds.map((p) => new Date(p.date).getTime() / ms);
        let valueSeries = ds.map((p) => p.value);
        let dayMin = Math.min.apply(Math, daySeries);
        let dayMax = Math.max.apply(Math, daySeries);
        let avg = mean(valueSeries)
        let sd = stdDev(valueSeries, avg)  //standard deviation
        let yMax = avg + sd * 4;
        let yMin = avg - sd * 3;

        if (this.trueZero || yMin < sd * 3) yMin = 0
        let dayRange = dayMax - dayMin;
        let xScale = svgWidth / dayRange;
        let yScale = svgHeight / (yMax - yMin);

        for (var i = 0; i < daySeries.length; i++) {
            this.plotPath += toX(daySeries[i]) + "," + toY(valueSeries[i]) + " "
        }

        //y-axeis captions
        let yInterval = interval(yMax - yMin, 10.0);
        if (yInterval < 0 && yInterval > 0.01) {
            // suppress more than 3 decimal places
            yInterval = Math.round(yInterval * 100) / 100
        }

        for (let y = 0; y <= yMax; y += yInterval) {
            if (y >= yMin) {
                let yy = (y > 0.001) ? Math.round(y * 1000) / 1000 : y
                this.yCaptions.push({ text: yy, y: toY(yy) });
            }
        }

        // x axis captionns
        let dateMin = new Date(ds[0].date);
        let dateMax = new Date(ds[ds.length - 1].date);
        let yearMin = dateMin.getFullYear();
        let yearMax = dateMax.getFullYear();
        let yearRange = yearMax - yearMin;
        let monthRange = yearMax * 12 + dateMax.getMonth() - (yearMin * 12 + dateMin.getMonth());

        if (monthRange < 5) {
            let incr = Math.floor(interval(dayRange, 14, [7, 2, 1]));
            if (incr < 1) incr = 1;
            let d = new Date(dateMin.getFullYear(), dateMin.getMonth())
            let om = d.getMonth()
            while (d < dateMax) {
                let m = d.getMonth()
                if (m != om) {
                    om = m
                    d = new Date(d.getFullYear(), m)
                    //this.xCaptions1.push({ text: "1", x: toX(d.getTime() / this.ms) });
                } else {
                    this.xCaptions1.push({ text: d.getDate(), x: toX(d.getTime() / ms) });
                }
                d = new Date(d.getTime() + incr * ms);
            }
            this.xCaptions2 = monthCaptions({ month: "short", year: "numeric" });
            if (
                this.xCaptions2.length == 0 ||
                this.xCaptions2[0].x > svgWidth / 5
            ) {
                this.xCaptions2.push({
                    text: dateMin.toLocaleDateString("default", { month: "short", year: "numeric" }), x: 0
                });
            }
        } else if (yearRange < 5) {
            this.xCaptions1 = monthCaptions({ month: "short" });
            this.xCaptions2 = yearCaptions();
            if (
                this.xCaptions2.length == 0 ||
                this.xCaptions2[0].x > svgWidth / 5
            ) {
                this.xCaptions2.push({ text: yearMin, x: 0 });
            }
        } else {
            this.xCaptions1 = yearCaptions();
        }

        return this

        function monthCaptions(format) {
            let capts = [];
            if (monthRange > 0) {
                let d = new Date(yearMin, 0);
                let incr = Math.floor(interval(monthRange, 10, [12, 6, 3, 2, 1]));
                if (incr < 1) incr = 1;
                while (d < dateMax) {
                    if (d >= dateMin) {
                        capts.push({
                            text: d.toLocaleDateString("default", format),
                            x: toX(d.getTime() / ms),
                        });
                    }
                    let m = d.getMonth() + incr;
                    let y = d.getFullYear();
                    if (m > 12) {
                        y += Math.floor(m / 12);
                        m = m % 12;
                    }
                    d = new Date(y, m);
                }
            }
            return capts;
        }

        function yearCaptions() {
            let capts = [];
            if (yearRange > 0) {
                let incr = Math.floor(interval(yearRange, 10));
                if (incr < 1) incr = 1;
                let d = new Date(yearMin, 0);
                while (d < dateMax) {
                    if (d >= dateMin) {
                        capts.push({ text: d.getFullYear(), x: toX(d.getTime() / ms) });
                    }
                    d = new Date(d.getFullYear() + incr, 0);
                }
            }
            return capts;
        }

        function interval(range, steps, defaults = [10, 5, 2, 1]) {
            var tempStep = range / steps;
            var magPow = Math.pow(10.0, Math.floor(Math.log10(tempStep)));
            var magMsd = Math.round(tempStep / magPow + 0.5);
            for (var i = 0; i < defaults.length - 1; i++) {
                if (magMsd > defaults[i + 1]) {
                    magMsd = defaults[i];
                    break;
                }
            }
            return magMsd * magPow;

        }

        function toX(day) {
            return (day - dayMin) * xScale;
        }

        function toY(val) {
            return svgHeight - (val - yMin) * yScale;
        }

        function mean(arr) {
            return arr.reduce((acc, val) => acc + val, 0) / arr.length;
        }

        function stdDev(arr, mean) {
            return arr.reduce((acc, val) => acc + Math.abs(val - mean), 0) / arr.length;
        };
    }
} 