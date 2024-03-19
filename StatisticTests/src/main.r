source("./src/libs/garch_models.r")
data <- rnorm(1000)
garch <- FitBestGarch(data)
print(garch)
