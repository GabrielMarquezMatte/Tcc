source("./libs/garch_models.r", chdir = TRUE)
data <- rnorm(1000)
garch <- FindBestArchModel(data)
rugarch::plot(garch$fit_bic)
