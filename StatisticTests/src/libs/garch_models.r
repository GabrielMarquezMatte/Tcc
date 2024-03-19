FitBestGarch <- function(R,
                       variance = c("sGARCH", "eGARCH", "gjrGARCH", "apARCH", "csGARCH"),
                       distributions = c("norm", "std", "ged", "snorm", "sstd", "sged", "jsu", "ghyp"),
                       garch_p = c(0, 1),
                       garch_q = c(0, 1),
                       arma_p = c(0, 1),
                       arma_q = c(0, 1),
                       criteria = c("Akaike", "Bayes", "Shibata", "Hannan-Quinn", "likelihood"),
                       n.ahead = 1,
                       conditional = TRUE,
                       ...) {
    tbl <- R %>%
        as.list() %>%
        tibble::enframe(.) %>%
        tidyr::crossing(variance, garch_p, garch_q, arma_p, arma_q, distributions) %>%
        dplyr::mutate(
            spec = purrr::pmap(
                .l = .,
                .f = ~ rugarch::ugarchspec(
                    variance.model     = list(model = ..3, garchOrder = c(..4, ..5)),
                    mean.model         = list(armaOrder = c(..6, ..7)),
                    distribution.model = ..8,
                    ...
                )
            ),
            model = purrr::map2(
                .x   = .data$value,
                .y   = .data$spec,
                .f   = purrr::possibly(~ rugarch::ugarchfit(spec = .y, data = .x), otherwise = NULL)
            ),
        )
    tbl <- tbl %>%
        dplyr::mutate(infocriteria = purrr::map(
            .x = .data$model,
            .f = purrr::possibly(~ rugarch::infocriteria(.x), otherwise = NULL)
        )) %>%
        dplyr::filter(.data$infocriteria != "NULL")
    return(tbl)
}
