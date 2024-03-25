import numpy as np
from scipy.optimize import minimize
from arch.univariate.base import ARCHModelResult
import yfinance as yf
from arch import arch_model
from matplotlib import pyplot as plt
import numba
import datetime as dt

def UnconditionalCovarianceAndCorrelation(resids: np.ndarray):
    covariance = np.cov(resids, rowvar=True)
    std_resid = np.sqrt(np.diag(covariance))
    return covariance, covariance / np.outer(std_resid, std_resid)

def CalculateQMatrix(alpha: float, beta: float, unconditional_corr: np.ndarray, prev_errors: np.ndarray, prev_q: np.ndarray):
    error_term = np.outer(prev_errors, prev_errors)
    return (1 - alpha - beta) * unconditional_corr + alpha * error_term + beta * prev_q

@numba.jit(nopython=True)
def ConditionalCorrelation(q_matrix: np.ndarray):
    q_diag = np.sqrt(np.diag(q_matrix))
    q_inv_diag = np.diag(1 / q_diag)
    return q_inv_diag @ q_matrix @ q_inv_diag

@numba.jit(nopython=True)
def CalculateAllCorrelations(returns: np.ndarray, volatilities: np.ndarray,
                             alpha: float, beta: float,
                             unconditional_corr: np.ndarray, unconditional_cov: np.ndarray,
                             errors: np.ndarray):
    n, T = errors.shape
    s_part = (1 - alpha - beta) * unconditional_corr
    prev_q = unconditional_corr
    conditional_correlations = np.zeros((n, n, T))
    conditional_covariances = np.zeros((n, n, T))
    conditional_correlations[:, :, 0] = unconditional_corr
    conditional_covariances[:, :, 0] = unconditional_cov
    pi_part = -n*0.5*np.log(2*np.pi)
    current_return = np.ascontiguousarray(returns[:, 0])
    log_likelihood = pi_part - 0.5 * (np.log(np.linalg.det(unconditional_cov)) + current_return.T @ np.linalg.inv(unconditional_cov) @ current_return)
    for t in range(1, T):
        prev_errors = errors[:, t-1]
        error_term = np.outer(prev_errors, prev_errors)
        q_matrix = s_part + alpha * error_term + beta * prev_q
        conditional_corr = ConditionalCorrelation(q_matrix)
        prev_q = q_matrix
        conditional_correlations[:, :, t] = conditional_corr
        current_return = np.ascontiguousarray(returns[:, t])
        diag_vol = np.diag(volatilities[t, :])
        covariance = diag_vol @ conditional_corr @ diag_vol
        log_likelihood += pi_part - 0.5 * (np.log(np.linalg.det(covariance)) + current_return.T @ np.linalg.inv(covariance) @ current_return)
        conditional_covariances[:, :, t] = covariance
    return conditional_covariances, conditional_correlations, log_likelihood

def LambdaDcc(x: np.ndarray, returns: np.ndarray, volatilities: np.ndarray, unconditional_corr: np.ndarray, unconditional_cov: np.ndarray, errors: np.ndarray):
    if x[0] + x[1] >= 1 or x[0] <= 0 or x[1] <= 0:
        return np.Inf
    return -CalculateAllCorrelations(returns, volatilities, x[0], x[1], unconditional_corr, unconditional_cov, errors)[2]

def FitDcc(returns: np.ndarray, arch_results: list[ARCHModelResult]):
    n, T = returns.shape
    errors = np.zeros((n, T))
    unconditional_cov, unconditional_corr = UnconditionalCovarianceAndCorrelation(returns)
    for i, result in enumerate(arch_results):
        errors[i] = result.std_resid
    alpha = 0.10
    beta = 0.85
    volatilities = np.sqrt(np.array([result.conditional_volatility for result in arch_results]).T)
    bounds = [(0, 1), (0, 1)]
    conds = [{'type': 'ineq', 'fun': lambda x: 1 - x[0] - x[1]}, {'type': 'ineq', 'fun': lambda x: x[0]}, {'type': 'ineq', 'fun': lambda x: x[1]}]
    res = minimize(lambda x: LambdaDcc(x, returns, volatilities, unconditional_corr, unconditional_cov, errors), [alpha, beta], bounds=bounds, constraints=conds)
    alpha, beta = res.x
    conditional_covariances, _, _ = CalculateAllCorrelations(returns, volatilities, alpha, beta, unconditional_corr, unconditional_cov, errors)
    return conditional_covariances

def main():
    tickers = ["^BVSP", "TRPL4.SA", "ITSA4.SA", "PETR4.SA", "VALE3.SA"]
    values = yf.download(tickers, start="2000-01-01", end="2023-01-01")['Adj Close']
    returns = np.log((values.pct_change().dropna()+1).values) * 100
    arch_results = [arch_model(returns[:, i]).fit(disp = False) for i in range(returns.shape[1])]
    alpha, beta, conditional_covariances, conditional_correlations, log_likelihood = FitDcc(returns, arch_results)
    print(alpha, beta, log_likelihood)
    correlation = [cov[0, 1] for cov in conditional_correlations]
    plt.plot(correlation)
    plt.show()

if __name__ == "__main__":
    main()